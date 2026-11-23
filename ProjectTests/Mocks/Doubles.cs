using System;
using System.Collections.Generic;
using GameSystem.Core.Abilities;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;
using Infra.Data;
using Infra.Display;

namespace ProjectTests.Mocks
{
    /// <summary>Fake: simplest implementation, does nothing.</summary>
    public class DisplayerFake : IDisplayer
    {
        public void Display(string line) { }
        public void Display(IDisplayable displayable) { }
        public void Write(string text) { }
    }

    /// <summary>Stub: throws when it receives the "bad" values.</summary>
    public class DisplayerStub : IDisplayer
    {
        private readonly string _badLine;
        private readonly IDisplayable _badDisplayable;

        public DisplayerStub(string badLine, IDisplayable badDisplayable)
        {
            _badLine = badLine;
            _badDisplayable = badDisplayable;
        }

        public void Display(string line)
        {
            if (_badLine == line)
                throw new Exception("This is a bad line!");
        }

        public void Display(IDisplayable displayable)
        {
            if (ReferenceEquals(_badDisplayable, displayable))
                throw new Exception("This is a bad displayable!");
        }

        public void Write(string text) { }
    }

    /// <summary>Mock: records everything it was asked to show.</summary>
    public class DisplayerMock : IDisplayer
    {
        /// <summary>One entry per Display call (a displayable is stored as its rendered text).</summary>
        public List<string> Lines { get; } = new List<string>();

        /// <summary>Text written without a line break (prompts).</summary>
        public List<string> Writes { get; } = new List<string>();

        public int DisplayLineCount { get; private set; }
        public int DisplayCount { get; private set; }

        public void Reset()
        {
            Lines.Clear();
            Writes.Clear();
            DisplayLineCount = 0;
            DisplayCount = 0;
        }

        public void Display(string line)
        {
            DisplayLineCount++;
            Lines.Add(line);
        }

        public void Display(IDisplayable displayable)
        {
            DisplayCount++;
            Lines.Add(displayable.Render());
        }

        public void Write(string text) => Writes.Add(text);
    }

    /// <summary>Stub of the input: returns scripted answers, then null (end of input).</summary>
    public class InputReaderStub : IInputReader
    {
        private readonly Queue<string> _answers = new Queue<string>();

        public InputReaderStub(params string[] answers)
        {
            Enqueue(answers);
        }

        public int Remaining => _answers.Count;

        public void Enqueue(params string[] answers)
        {
            foreach (var answer in answers)
                _answers.Enqueue(answer);
        }

        public void Clear() => _answers.Clear();

        public string? ReadLine() => _answers.Count > 0 ? _answers.Dequeue() : null;
    }

    /// <summary>Fake storage that keeps the repository in memory (no files).</summary>
    public class InMemoryStorage : IRepositoryStorage
    {
        private Repository _repository = new Repository();

        public string Location => "memory";
        public int SaveCalls { get; private set; }

        public Repository Load() => _repository;

        public void Save(Repository repository)
        {
            _repository = repository;
            SaveCalls++;
        }
    }

    /// <summary>Mock ability: counts executions and remembers the arguments.</summary>
    public class AbilityMock : IAbility
    {
        public AbilityMock(string name = "Mock ability")
        {
            Name = name;
        }

        public string Name { get; }
        public int ExecuteCount { get; private set; }
        public Character? LastSource { get; private set; }
        public Character? LastTarget { get; private set; }

        public void Execute(Character source, Character target, IGameLogger logger)
        {
            ExecuteCount++;
            LastSource = source;
            LastTarget = target;
        }
    }
}