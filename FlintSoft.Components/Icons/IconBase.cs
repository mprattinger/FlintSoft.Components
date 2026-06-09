using Microsoft.AspNetCore.Components;


namespace FlintSoft.Components.Icons;

public abstract class IconBase : MyComponentBase
{
    [Parameter]
    public EventCallback OnClicked { get; set; }

    [Parameter]
    public bool StopPropagation { get; set; } = false;

    [Parameter]
    public override string? Class { get; set; } = null;

    [Parameter]
    public string? Title { get; set; }

    protected override string? ClassValue
    {
        get
        {
            return Class;
        }
    }
}