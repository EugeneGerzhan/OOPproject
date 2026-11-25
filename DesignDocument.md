# Design Document: Character Battler

A console application in C# (.NET 8) that combines a structured text editor, a character catalog with a remote
database, and a turn-based text game. The document describes the structure of the application and, in detail,
the command line interface (CLI).

## Contents
1. Introduction
2. Requirements
3. Architecture
4. Domain model
5. Data design
6. Remote catalog
7. Command line interface (detailed)
8. Theme 2: text game
9. Theme 3: structured text editor
10. Testing
11. Limitations and future work
12. Build and run

## 1. Introduction

### 1.1 Purpose
The application lets a user work in a terminal with three subsystems that share one command line:
a catalog of characters (local and imported from a remote service), a turn-based text game, and an editor
of structured (hierarchical) text documents.

### 1.2 Themes of the project
| Theme | Name | Mode of the application |
|---|---|---|
| 1 | Character catalog | `--chars` (commands `create`, `add`, `ls`, `remote`, `sheet`, `save`, `load`) |
| 2 | Text game | `--chars` (commands `game ...`) |
| 3 | Own theme: structured text editor | `--text` (commands `add`, `rm`, `cp`, `cd`, `print`, `undo`, ...) |

### 1.3 Scope
In scope: the CLI and everything it needs from Core and Infra (some parts are simple but real implementations).
Out of scope for this version: a graphical interface, network play, saving of a battle in progress (see section 11).

### 1.4 Glossary
| Term | Meaning |
|---|---|
| Strategy (of the CLI) | A set of commands that belong together; plugged into the `Cli` |
| Selector | The name of a command (the first word of a typed line) |
| Renderable / renderer | An object that carries data / the object that turns that data into text |
| Memento | An opaque snapshot of the document used by `undo` |

## 2. Requirements

### 2.1 Functional requirements
**Theme 1: character catalog**
| Requirement | Implementation |
|---|---|
| Update and show the list of all available characters | `remote ls char` asks the service every time, so the list is always current; options `--find <text>`, `--limit <n>`; `ls char` lists the characters of the game |
| Show general information, including a link to the graphics | `remote show char <id>`: title, vision, weapon type, rarity, nation, affiliation, description, talents and the `Image` link |
| Create a new character through a dialog | `create char` (name, class, optional id), also `create item` and `create ability` |
| Save user data | `save <name>`, `load <name>`, `saves` (characters, items, abilities and their links) |
| Extra | `remote import char <id>` and `remote import weapon <id>` bring remote data into the game; `add item` equips; `sheet <character>` prints a character sheet |

**Theme 2: text game**
| Requirement | Implementation |
|---|---|
| Choice of characters | `game roster` lists the characters that can play (defeated ones are marked) |
| Start the game | `game start <A> <B> [...]` (two or more characters, by name or id) |
| Actions in the main loop | `game attack <target>`, `game defend`, `game heal [<target>] [--amount <hp>]`, `game ability <target> [--id <ability>]`, `game status`, `game stop` |
| Save state and results | State of the characters: `save` / `load`. Results of finished battles are saved automatically; `game results` shows them |
| Step history | `game history` shows every action with the messages it caused (also for the last finished or abandoned battle) |
| Save the state of the game | `game save <name>` saves the characters, their stats and the battle in progress; `game load <name>` restores them |
| Load new characters | `remote import char <id>`, `create char`, `game load <name>` |

**Theme 3: structured text editor**
| Requirement | Implementation |
|---|---|
| Operations of the theme | `add` (heading or paragraph), `rm`, `cp` (copy a subtree), `cd`, `up`, `undo` |
| Output of the information the theme needs | `pwd` (path), `print` of the current element or the whole document, with ids (`--id`) and in a chosen format (`--format plain\|html\|markdown`) |
| Persistence | `save <name>`, `load <name>`, `saves` |

### 2.2 Non-functional requirements
* **Extensibility:** a new command, strategy, output format or renderer is added without changing existing code.
* **Testability:** no direct calls of `Console` outside the infrastructure layer; the console and the network are replaceable.
* **Robustness:** an error in a command is reported to the user and never ends the program.

## 3. Architecture

### 3.1 Layers
```mermaid
flowchart LR
    subgraph P["Presentation layer"]
        CLI["CLI (project SOLID)<br/>Cli, strategies, renderers, Presenter"]
        GUI["GUI (future)"]
    end
    subgraph C["Business-logic layer (Core)"]
        TEXT["Text<br/>OOPTextProject"]
        CHARS["Characters<br/>GameSystem.Core"]
        ENGINE["Engine<br/>GameSystem.Engine<br/>(Game, Battle)"]
    end
    subgraph I["Infrastructure layer (Infra)"]
        DISPLAY["Display<br/>IDisplayer, IRenderable"]
        DATA["Data<br/>JSON, SaveManager"]
        WEB["WebApi<br/>GenshinApiClient"]
    end
    CLI --> TEXT
    CLI --> CHARS
    CLI --> ENGINE
    GUI -.-> CHARS
    ENGINE --> CHARS
    TEXT --> DISPLAY
    CHARS --> DISPLAY
    CHARS --> DATA
    CHARS --> WEB
    TEXT --> DATA
    CLI --> DISPLAY
    CLI --> DATA
    CLI --> WEB
    WEB --> REMOTE[("genshin.jmp.blue")]
    DATA --> FILE[("saves.json")]
```
Dependencies point only downwards: Core knows nothing about the CLI, Infra knows nothing about Core or the CLI.

### 3.2 Projects
| Project | Responsibility |
|---|---|
| `SOLID` | CLI: `Cli`, command strategies, renderers, `Presenter`, entry point |
| `OOPTextProject` | Structured text system |
| `GameSystem.Core` | Characters, items, abilities, DTOs, import from the remote service |
| `GameSystem.Engine` | `Game` and the turn-based `Battle` |
| `Infra` | `Display` (output), `Data` (JSON persistence), `WebApi` (HTTP client) |
| `OOPProject.Tests` | Unit tests of all layers |

### 3.3 Design patterns in use
| Pattern | Where |
|---|---|
| Facade | `Cli` (CLI), `TextFactory` (text system), `ListEditor`-like facades hide subsystems behind few calls |
| Strategy | Command strategies of the CLI; abilities of characters (`IAbility`); `IRenderer<T>`; `IRepositoryStorage`; `IGameDataApi` |
| Command | `ICommand`, `DelegateCommand`, `UndoableCommand` |
| Composite | The document tree (`Root`, `Heading`, `Paragraph`) |
| Visitor | `TextDto` (formatting) and `TextMapper` (conversion to data) |
| Iterator | `Container.SelfAndDescendants()` |
| Memento | `TextMemento`, `UndoHistory`, `TextFactory.CreateMemento/Restore` |
| Abstract Factory | Output formats of documents (`ITextFormatFactory`: plain, HTML, Markdown) |
| Builder | `DocumentBuilder` (also used for character sheets) |
| Prototype | Copying subtrees (`cp`) |
| Template Method | `EntityDto` (short or detailed form of game DTOs) |

## 4. Domain model

### 4.1 Characters
`Character` has health, armor, attack power, an equipped `Item`, a unique ability and learned abilities.
Concrete classes: `HalfOrcBarbarian`, `Monk`, and `ApiCharacter` (created from the remote database).
Abilities implement `IAbility`: `RageAbility`, `FlurryOfBlowsAbility`, `CustomAbility`.
`GameState` holds three registries (characters, items, abilities) whose objects are addressed by id or by name.
`GameSession` holds the current `GameState`, so all strategies see the same state, also after `load`.

### 4.2 Structured text
```mermaid
classDiagram
    direction LR
    class TextFactory {
        +AddHeading(string)
        +AddParagraph(string)
        +AddCopy(IText, string)
        +TryCd(string)
        +TryRemove(IText)
        +CreateMemento()
        +Restore(TextMemento)
        +GetWholeView(bool, ITextFormatFactory)
    }
    class IText {
        <<interface>>
        +Id
        +Name
        +Accept~T~(ITextVisitor~T~)
    }
    class Container {
        <<abstract>>
        +Children
        +SelfAndDescendants()
    }
    class Root
    class Heading
    class Paragraph
    class ITextVisitor~T~ {
        <<interface>>
    }
    class TextDto
    class ITextFormatFactory {
        <<interface>>
    }
    class UndoHistory
    class TextMemento
    TextFactory o-- Root
    IText <|.. Container
    IText <|.. Paragraph
    Container <|-- Root
    Container <|-- Heading
    Container o-- "*" IText
    ITextVisitor~T~ <|.. TextDto
    TextDto ..> ITextFormatFactory
    UndoHistory o-- "*" TextMemento
    TextFactory ..> TextMemento : creates
```
Every element has a unique id (the root has id 0). `TextFactory` keeps the current position, so commands
such as `add` and `cd` work relative to it.

### 4.3 Battle
`Battle` keeps the order of turns, the round number, defense and the winner. Rule violations are reported with
`InvalidOperationException`; the turn is not spent. `Battle.CreateView()` returns the state as data
(`BattleBoard`), without any text.

## 5. Data design
All saved data live in one JSON file, `saves.json` (class `Repository`):

| Section | Content |
|---|---|
| `CharacterSaves` | Named saves: characters, items, abilities and the links between them |
| `TextSaves` | Named saves: the document as a tree of nodes |
| `BattleResults` | Winner, participants, rounds, actions and time of every finished battle |
| `CharacterSaves[..].Battle` | A battle in progress: participants, turn order, round, actions and the history of steps |

The storage is replaceable (`IRepositoryStorage`); `SaveManager` offers named saves on top of it. An older file
without `BattleResults` still loads.

## 6. Business logic: character catalog (Theme 1)

### 6.1 Purpose
The business logic of the catalog turns the JSON of the remote service into models that the graphical interface
can show without any processing. The GUI only has to make a view model out of a `CharacterCard`.

### 6.2 Pipeline
| Step | Class | Result |
|---|---|---|
| Request | `GenshinApiClient` (Infra.WebApi) | JSON text; network and status errors become `RemoteApiException` |
| Parsing | `GenshinJsonParser` | `RemoteCharacter` / `RemoteWeapon` (tolerant: only the name is required) |
| Catalog | `CharacterCatalog` (Core) | `CharacterCard` objects in memory |
| Game object | `RemoteImporter` | `ApiCharacter`, `Item` (see 8.7) |

### 6.3 The models for the GUI
`CharacterCard` holds the id, name, title, vision, weapon type, rarity, nation, affiliation, description, the link to
the image, and lists of `TalentCard` (skills and passives). Nothing in it is null.

### 6.4 `CharacterCatalog`
| Operation | Meaning |
|---|---|
| `RefreshAsync(maxParallel)` | Loads the list of ids, then the card of every character, several requests at a time. A character that cannot be loaded is reported in `CatalogRefreshResult.Failed`; if the list itself cannot be loaded, the old cards stay |
| `GetAsync(id)` | The card from memory, or from the service (then it is kept) |
| `Find(id)` | A loaded card or `null` |
| `Search(filter)` | `CatalogFilter`: text (name, title, id), vision, weapon type, minimal rarity, sort (name, rarity, vision) |

The link to the graphics is the `image` field of the answer; when it is absent, it is built from the endpoint
`characters/{id}/card`. If the service is down, the offline sample data are used (`--offline`).
## 7. Command line interface (detailed)

### 7.1 Purpose and principles
The CLI belongs to the presentation layer. It accepts a line typed by the user, finds the command, runs it
through the logic of Core and shows the result. It contains no game rules: the rules are in `Battle`,
`Character` and `TextFactory`. Commands of one subsystem are grouped into a **strategy**; the CLI is parametrized
with strategies, so a subsystem can be added or removed without touching the others.

### 7.2 Structure
```mermaid
classDiagram
    direction LR
    class Cli {
        +UseStrategy(ICommandStrategy)
        +ExecCommand(string)
        +Display~T~(IRenderable~T~)
        +Strategies
    }
    class IArgsParser {
        <<interface>>
        +ParseArgs(string) ParsedCommand
    }
    class ArgsParser
    class Args {
        +Positional
        +Options
        +Has(string)
        +Get(string)
        +Joined(int)
    }
    class ICommandStrategy {
        <<interface>>
        +GetCommandSelectors()
        +RegisterCommand(string, ICommand)
        +ExecCommand(string, Args)
    }
    class CommandStrategyBase {
        <<abstract>>
        #Register(string, Action)
    }
    class ICommand {
        <<interface>>
        +Execute(string[], Dictionary)
    }
    class DelegateCommand
    class UndoableCommand
    class IRenderable~T~ {
        <<interface>>
        +Data
        +UseRenderer(IRenderer~T~)
        +Render(IRenderer~T~)
        +Render()
    }
    class IRenderer~T~ {
        <<interface>>
        +Render(T)
    }
    class Renderable~T~
    class BattleBoardRenderer
    class TextCliHandler
    class CharactersCliHandler
    class RemoteCliHandler
    class GameStrategy
    class SystemStrategy
    Cli *-- IArgsParser : 1
    IArgsParser <|.. ArgsParser
    ArgsParser ..> Args : creates
    Cli o-- "*" ICommandStrategy
    ICommandStrategy <|.. CommandStrategyBase
    CommandStrategyBase <|-- TextCliHandler
    CommandStrategyBase <|-- CharactersCliHandler
    CommandStrategyBase <|-- RemoteCliHandler
    CommandStrategyBase <|-- GameStrategy
    CommandStrategyBase <|-- SystemStrategy
    CommandStrategyBase o-- "*" ICommand
    ICommand <|.. DelegateCommand
    ICommand <|.. UndoableCommand
    IRenderable~T~ <|.. Renderable~T~
    Renderable~T~ o-- "1" IRenderer~T~
    IRenderer~T~ <|.. BattleBoardRenderer
    Cli ..> IRenderable~T~ : Display
```

### 7.3 Components
| Component | Responsibility |
|---|---|
| `Cli` | Facade with three operations: `UseStrategy`, `ExecCommand`, `Display<T>`. Owns the parser (composition) and the list of strategies (aggregation) |
| `IArgsParser` / `ArgsParser` | Turns a raw line into `ParsedCommand` (selector and `Args`) |
| `Args` | Object form of the arguments: positional words and options (case-insensitive names) |
| `ICommandStrategy` | A set of commands: `GetCommandSelectors`, `RegisterCommand`, `ExecCommand` |
| `CommandStrategyBase` | Table "selector to command"; concrete strategies register commands in their constructors |
| `ICommand` and its implementations | A command as an object; `DelegateCommand` wraps a method, `UndoableCommand` adds undo |
| `IRenderable<T>` / `IRenderer<T>` | Data that can be shown / the strategy that turns the data into text |
| `Presenter` (`IPresenter`) | The only way strategies read from and write to the user; works over `IDisplayer` and `IInputReader` |
| `SystemStrategy` | The command `commands`, which lists the selectors of every strategy |
| `CommandInterpreter` | A thin adapter of the old API on top of `Cli` (kept so existing code and tests keep working) |

Correspondence with the reference design: `useStrategy` is `Cli.UseStrategy`, `execCommand` is `Cli.ExecCommand`,
`display` is `Cli.Display<T>`, `ArgParser.parseArgs` is `IArgsParser.ParseArgs`. The second variant of the
rendering design is used: the renderable owns its renderer, and the CLI supplies the renderer. A deviation:
`RegisterCommand` takes an `ICommand` object (a method is wrapped in `DelegateCommand`) instead of a bare function.

### 7.4 Execution of a command
```mermaid
sequenceDiagram
    actor User
    participant Main as Program (main loop)
    participant Cli
    participant Parser as ArgsParser
    participant S as ICommandStrategy
    participant Cmd as ICommand
    participant Core as Core / Engine
    User->>Main: game attack "Shi Fuu"
    Main->>Cli: ExecCommand(line)
    Cli->>Parser: ParseArgs(line)
    Parser-->>Cli: ParsedCommand(game, Args)
    Cli->>S: ExecCommand("game", Args)
    S->>Cmd: Execute(positional, options)
    Cmd->>Core: Battle.Attack(target)
    Core-->>Cmd: messages via IGameLogger
    Cmd->>Cli: Display(IRenderable BattleBoard)
    Cli-->>User: text via IPresenter
```

### 7.5 Modes and start-up
| Argument | Mode |
|---|---|
| `--text` | Structured text editor (strategy `TextCliHandler`) |
| `--chars` | Characters, remote catalog and game (strategies `CharactersCliHandler`, `RemoteCliHandler`, `GameStrategy`) |
| `--chars --offline` | The same, with built-in sample data instead of the remote service |
| none | A dialog asks which mode to start |

`SystemStrategy` is plugged in in both modes. The prompt is `>`; `exit` ends the program.

### 7.6 Parsing rules
The first word is the selector. The other words are positional arguments and options. `--name value` is an
option with a value, `--flag` is a flag (the value is empty). Text in quotes (`"..."`, `'...'`, `“...”`, `«...»`)
is one word. A quote opens a quoted text only at the beginning of a word, so an apostrophe inside a word
(`п'ять`, `don't`) stays an ordinary character. Names of options are case-insensitive.

### 7.7 Error handling
* An unknown command is reported (`Unknown command: ...`).
* An exception inside a command is caught by `Cli` and shown as `[Error executing command]: ...`; the program continues.
* Breaking a rule of the game is shown by `GameStrategy` as a plain message (it is not a failure of the program).
* Network errors and unexpected answers of the service are `RemoteApiException` with a readable message.
* Commands that change the document ask for confirmation where data can be lost (`rm`, `load`).

### 7.8 Output
Objects that present themselves (DTOs of the game, `TextDto`) implement `IDisplayable`. Data without its own
presentation (the battle board, the list of playable characters, the results) are passed as `Renderable<T>`:
the core provides the data and the CLI sets the renderer (`BattleBoardRenderer`, `RosterRenderer`,
`ResultsRenderer`). A GUI would provide its own renderers for the same data. All reading and writing goes through
`IPresenter`, which uses `IDisplayer` and `IInputReader`; the console is replaced by test doubles in tests.

### 7.9 Commands
| Strategy | Commands | Kind |
|---|---|---|
| `TextCliHandler` | `pwd`, `print`, `add`, `rm`, `up`, `cd`, `cp`, `undo`, `save`, `load`, `saves`, `help` | transform, output, persistence |
| `CharactersCliHandler` | `create`, `add`, `act`, `ls`, `save`, `load`, `saves`, `help` | transform, output, persistence |
| `RemoteCliHandler` | `remote ls\|show\|import`, `sheet` | infrastructure, output |
| `GameStrategy` | `game roster\|start\|status\|attack\|defend\|heal\|ability\|stop\|results` | transform, output |
| `SystemStrategy` | `commands` | output |

### 7.10 Extension points
* **A new command:** write a method in a strategy and add `Register("name", Method)`.
* **A new strategy:** derive from `CommandStrategyBase`, register commands in the constructor and plug it in with `cli.UseStrategy(...)` in `Program`.
* **A new way to show data:** implement `IRenderer<T>`; the core is not changed.
* **A new document format:** implement `ITextFormatFactory` and add it to `TextFormats`.
* **Undo for a command:** wrap it in `UndoableCommand`.
* **A graphical interface:** a new project in the presentation layer that reuses Core, Data and WebApi.

## 8. Business logic: game mechanics (Theme 2)

### 8.1 Structure
```mermaid
classDiagram
    direction LR
    class Battle {
        +Round
        +Current
        +Actions
        +History
        +Attack(Character)
        +Defend()
        +Heal(Character, int)
        +UseAbility(Character, IAbility)
        +GetProgress() BattleProgress
        +Restore(...)$ Battle
        +CreateView() IRenderable
    }
    class TurnRecord {
        +Number
        +Round
        +Actor
        +Action
        +Target
        +Messages
    }
    class BattleProgress {
        +Round
        +Queue
        +QueueIndex
        +Actions
        +History
    }
    class GameRules {
        +MinimumDamage
        +DefenseArmorMultiplier
        +DefenseResistBonus
        +MaxResist
        +MagicScale
        +BaseSpeed
    }
    class DamageCalculator {
        +Calculate(...)$ int
    }
    class AttackProfile {
        +Kind
        +ResistPenetration
    }
    class Character {
        +Health
        +Armor
        +AttackPower
        +Resist
        +Speed
        +Attack(Character, IGameLogger, AttackProfile)
        +Heal(int, IGameLogger)
        +PrepareDefense(IGameLogger)
    }
    class ApiCharacter {
        +Profile
    }
    class CharacterProfile
    class Item
    class IAbility {
        <<interface>>
        +Execute(Character, Character, IGameLogger)
    }
    class CustomAbility {
        +Kind
        +ResistPenetration
    }
    class GameState
    class CharacterSave
    class BattleStateData
    Battle o-- "2..*" Character
    Battle o-- "*" TurnRecord
    Battle ..> GameRules
    Battle ..> BattleProgress : saves / restores
    Character ..> DamageCalculator
    DamageCalculator ..> AttackProfile
    DamageCalculator ..> GameRules
    Character <|-- ApiCharacter
    ApiCharacter --> CharacterProfile
    Character --> "0..1" Item
    Character o-- "*" IAbility
    IAbility <|.. CustomAbility
    GameState o-- "*" Character
    CharacterSave --> "0..1" BattleStateData
```

| Reference design | Code |
|---|---|
| `Game` (`setupGame`, `startGame`) | `Battle` (constructor / `Restore`), `GameStrategy` in the CLI |
| `GameConfig` (loaded from persistence) | `CharacterSave` with `BattleStateData` |
| `Turn` (`act`, `beginTurn`) | `TurnRecord` (one action); the start of a round builds the queue |
| `Character` (`attack`, `heal`, `action`, `checkStatus`) | `Character` (`Attack`, `Heal`, `UseAbility`, `IsAlive`) |
| `CharacterProfile` | `CharacterProfile` of `ApiCharacter` (data of the remote database) |
| `Item`, `Ability` | `Item`, `IAbility` / `CustomAbility` |

### 8.2 Characteristics
| Characteristic | Meaning |
|---|---|
| Health (`Health`, `MaxHealth`) | How alive the character is; 0 means defeated |
| Damage (`AttackPower`) | The damage it deals; items and abilities add to it |
| Armor | Flat defense: how much damage is blocked directly |
| Resist (0..1) | The fraction of damage that is ignored, at most 90% counts |
| Speed (default 100) | Initiative, see 8.4 |

Items add to health, armor and attack. Abilities raise the attack for their hits. Stats of characters from the remote
database are generated from ranges that depend on rarity (the service has no stats).

### 8.3 Damage formulas
```
Notation: A = attack power (with items), AR = armor of the target, R = resist of the target,
          P = resist penetration of the attack (0 for ordinary attacks), M = MinimumDamage (1),
          S = MagicScale (0.75), K = 2 (defense armor multiplier), D = 0.25 (defense resist bonus)

Physical attack
    armor_eff   = AR x (K if the target defends else 1)
    base        = max(M, A - armor_eff)
    resist_eff  = clamp(R - P, 0, 0.9)
    damage      = max(M, round(base x (1 - resist_eff)))

Magic attack (ignores armor)
    base        = max(M, A x S)
    resist_eff  = clamp(R - P + (D if the target defends else 0), 0, 0.9)
    damage      = max(M, round(base x (1 - resist_eff)))

Healing
    health      = min(MaxHealth, health + amount)        (resist does not apply)
```
```mermaid
flowchart TD
    A["attack, armor, resist, defending?, kind, penetration"] --> K{"Kind?"}
    K -- Physical --> P1["armor_eff = armor x (2 if defending else 1)"]
    P1 --> P2["base = max(MinDamage, attack - armor_eff)"]
    K -- Magic --> M1["base = max(MinDamage, attack x MagicScale)"]
    M1 --> M2["if defending: resist + 0.25"]
    P2 --> R["resist_eff = clamp(resist - penetration, 0, 0.9)"]
    M2 --> R
    R --> F["damage = max(MinDamage, round(base x (1 - resist_eff)))"]
```
Rounding is "away from zero". The numbers are the constants of `GameRules`, so the balance is changed in one place.
Example: attack 20 against armor 8 gives 12; against resist 25% it gives 9; a magic attack with the same attack gives 15
and does not care about the armor. Deviation from the textbook formula: a hit that is not completely blocked deals at
least `MinimumDamage` (1, "chip damage"); with `MinimumDamage = 0` armor can block a hit completely.

**Abilities.** `CustomAbility` makes N hits with a temporary attack bonus; each hit uses the formula of its kind
(physical or magic) and may ignore a part of resist. The talents of imported characters become: normal attack, one
physical hit; skill, two magic hits (+3 attack); burst, three magic hits (+6 attack, ignores 10% of resist).

**Defense.** `Defend` uses the action of the turn. Until the start of the next round the armor against physical
attacks doubles and the resist against magic grows by 25 points.

### 8.4 Turns, rounds and speed
```mermaid
flowchart TD
    S["Start of a round"] --> Q["Build the queue: every living character gets max(1, speed div 100) slots at times k x 100 / speed"]
    Q --> O["Sort by time, then by position in the party"]
    O --> N{"Next slot"}
    N -- "actor is defeated" --> N
    N -- "actor is alive" --> ACT["The actor performs one action"]
    ACT --> W{"At most one character alive?"}
    W -- yes --> END["Battle over: winner, result saved"]
    W -- no --> E{"Queue finished?"}
    E -- no --> N
    E -- yes --> S
```
| Speed | Actions per round | Times |
|---|---|---|
| 90 | 1 | 1.11 (after the speed-100 characters) |
| 100 | 1 | 1.00 |
| 150 | 1 | 0.67 (before the speed-100 characters) |
| 200 | 2 | 0.50 and 1.00 |

Example: A (speed 200), B (100), C (150) act in the order A, C, A, B. With equal speeds the order is the order of the
party. Defense is reset at the start of every round.

### 8.5 Actions and rules
| Action | Rule |
|---|---|
| `attack <target>` | Physical attack on a living opponent (not itself, not a stranger) |
| `defend` | See 8.3 |
| `heal [<target>] [--amount n]` | Heals a living participant (default: itself, 15 HP) |
| `ability <target> [--id <ability>]` | The unique ability, or an ability the character has learned |
| End of the battle | At most one character is left: it wins (or a draw if nobody is left) |

A rejected action does not spend the turn and is not written to the history.

### 8.6 History and saving
Every action is a `TurnRecord`: number, round, actor, action, target and all messages it caused (damage, defeat).
`game history` shows them; after the end or `game stop` the history of the last battle stays available.

`game save <name>` writes a `CharacterSave` with the characters (including resist, speed, items, abilities) and, when a
battle is running, a `BattleStateData`:
```json
"Battle": {
  "ParticipantIds": [1, 2],
  "Round": 1,
  "Queue": [0, 1],
  "QueueIndex": 1,
  "Actions": 1,
  "History": [ { "Number": 1, "Round": 1, "Actor": "Growler", "Action": "attack",
                 "Target": "Shi Fuu", "Messages": ["Growler attacks Shi Fuu for 12 damage."] } ]
}
```
The queue is saved as it is, so a round in the middle (also with extra actions) is continued exactly. `game load`
builds everything first and replaces the state only after the user confirms, so a damaged save changes nothing.
The result of a finished battle is appended to `BattleResults`.

### 8.7 New characters
`remote import char <id>` loads a character of the remote database (stats are generated for its rarity, talents become
abilities), `create char` makes one through a dialog, `game load <name>` brings back a saved party. All of them appear in
`game roster`.

### 8.8 Not included
Status effects, buffs and debuffs (the "bonus" and "debuff" terms of the extended magic formula), critical hits and
elements are not part of this version; `GameRules` and `AttackProfile` are the places where they would be added.

## 9. Theme 3: structured text editor

### 9.1 Description
A document is a tree: a root (the title), headings (containers) and paragraphs (leaves). The user moves
around the tree like in a file system and changes it with commands. The same text system builds the character
sheets of Theme 1 (`sheet <character> --format html`).

### 9.2 Commands
| Command | Meaning |
|---|---|
| `pwd` | Current path, for example `/Chapter 1` |
| `add <container\|leaf> <heading\|paragraph> ["text"]` | Add an element (a dialog asks for the text when it is missing); `add heading "x"` is a shortcut. A new heading becomes the current one |
| `cd <path>` / `cd --id <id>` | Go to a container by path (`/a/b`, `../x`) or by id |
| `up` | Go to the parent |
| `rm [<name>]` / `rm --id <id>` | Remove an element after confirmation; without a name the current element |
| `cp <name> [--as <new name>]` / `cp --id <id>` | Copy an element with all its content (new ids) |
| `print [--whole] [--id] [--format plain\|html\|markdown]` | Show the current element or the whole document, optionally with ids and in a format |
| `undo` | Undo the last change made by `add`, `rm`, `cp` or `load` |
| `save <name>`, `load <name>`, `saves` | Save, restore and list documents |

### 9.3 Example session
```
> add heading "Chapter 1"
Heading 'Chapter 1' added (id 1). Current path: /Chapter 1
> add paragraph "Hello"
Paragraph added (id 2).
> up
Moved up to parent element. Current path: /
> cp "Chapter 1" --as "Chapter 2"
Copied Heading 'Chapter 1' (id 1) as 'Chapter 2' (id 3, 2 element(s)).
> print --whole --id
[0] Title: Main Document
    [1] Chapter 1
        [2] Hello
    [3] Chapter 2
        [4] Hello
> print --whole --format markdown
# Main Document

## Chapter 1

Hello

## Chapter 2

Hello
> undo
Undone. Current path: / (2 more step(s) can be undone).
```

### 9.4 Design notes
* Output formats are an Abstract Factory (`ITextFormatFactory`); `TextDto` is a visitor that walks the tree.
* `DocumentBuilder` builds a document step by step and refuses an unfinished one.
* `undo` takes a snapshot (`TextMemento`) before every command that changes the document and stores it in `UndoHistory` only if the document really changed (`TextFactory.Revision`).
* Moving around the document (`cd`, `up`) and printing are not steps of the history.

## 10. Testing
Unit tests (xUnit with Moq) cover every layer: the text system, the characters, `Battle`, the CLI facade, the
parser, the strategies, the renderers, the game and text commands, the JSON storage and the WebApi client.
The console and the network are replaced by test doubles (`DisplayerMock`, `InputReaderStub`,
`StubHttpMessageHandler`, `FakeGameDataApi`, `InMemoryStorage`). Run them with `dotnet test`.

## 11. Limitations and future work
* Status effects (buffs, debuffs) are not implemented; the saved battle continues exactly, but only one battle can be saved per save slot.
* There is no redo, and `undo` is available only in the text mode.
* `help` describes the commands of its own strategy; `commands` lists all of them.
* The remote service may be unavailable; `--offline` is the fallback.
* Planned: a graphical interface, saving of a battle in progress, `export` of a document to a file.

## 12. Build and run
Requirements: .NET 8 SDK.
```
dotnet build
dotnet test
dotnet run --project SOLID -- --text
dotnet run --project SOLID -- --chars
dotnet run --project SOLID -- --chars --offline
```