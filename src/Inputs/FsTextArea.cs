using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FlintSoft.Components;

public class FsTextArea : FsText
{
    [Parameter] public int Rows { get; set; } = 3;

    protected override void RenderControl(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "textarea");
        AddControlAttributes(builder);
        builder.AddAttribute(38, "rows", Rows);
        builder.AddAttribute(39, "placeholder", " ");
        AddTextBinding(builder);
        CaptureControlElement(builder, 60);
        builder.CloseElement();
    }
}
