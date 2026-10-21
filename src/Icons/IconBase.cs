using Microsoft.AspNetCore.Components;


namespace FlintSoft.Components.Icons;

public abstract class IconBase : ComponentBase
{
    [Parameter]
    public string? Id { get; set; }

    [Parameter]
    public EventCallback OnClicked { get; set; }

    [Parameter]
    public bool StopPropagation { get; set; } = false;

    [Parameter]
    public virtual string? Class { get; set; } = null;

    [Parameter]
    public string? Style { get; set; } = null;

    [Parameter]
    public string? Title { get; set; }

    protected abstract string? ClassValue
    {
        get;
    }

    protected abstract string? StyleValue
    {
        get;
    }
}