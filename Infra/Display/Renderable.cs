using System;

namespace Infra.Display;

/// <summary>
/// Strategy that turns data of type <typeparamref name="T"/> into text.
/// The owner of the data does not know how it is shown: the presentation layer (CLI, later GUI) provides the renderer.
/// </summary>
public interface IRenderer<in T>
{
    string Render(T data);
}

/// <summary>
/// An object that carries data and the renderer that presents it. It is an <see cref="IDisplayable"/>,
/// so everything that can show a displayable (the displayer, the presenter) can show it too.
/// </summary>
public interface IRenderable<T> : IDisplayable
{
    /// <summary>The data to present.</summary>
    T Data { get; }

    /// <summary>True when a renderer is set, so <see cref="IDisplayable.Render"/> can be called.</summary>
    bool HasRenderer { get; }

    /// <summary>Sets (or replaces) the renderer used by <see cref="IDisplayable.Render"/>.</summary>
    void UseRenderer(IRenderer<T> renderer);

    /// <summary>Presents the data with another renderer without changing the own one.</summary>
    string Render(IRenderer<T> renderer);
}

/// <summary>The default implementation of <see cref="IRenderable{T}"/>.</summary>
public class Renderable<T> : IRenderable<T>
{
    private IRenderer<T>? _renderer;

    public Renderable(T data, IRenderer<T>? renderer = null)
    {
        Data = data;
        _renderer = renderer;
    }

    public T Data { get; }

    public bool HasRenderer => _renderer != null;

    public void UseRenderer(IRenderer<T> renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public string Render(IRenderer<T> renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.Render(Data);
    }

    public string Render()
    {
        return _renderer?.Render(Data)
            ?? throw new InvalidOperationException("The renderable has no renderer. Call UseRenderer first.");
    }
}