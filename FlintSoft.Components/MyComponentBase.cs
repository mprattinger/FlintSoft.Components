using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlintSoft.Components;

public abstract class MyComponentBase : ComponentBase
{
    private ElementReference _ref;

    public ElementReference Element
    {
        get => _ref;
        protected set
        {
            _ref = value;
        }
    }

    protected abstract string? ClassValue
    {
        get;
    }

    protected abstract string? StyleValue
    {
        get;
    }

    [Parameter]
    public string? Id { get; set; }

    [Parameter]
    public virtual string? Class { get; set; } = null;

    [Parameter]
    public virtual string? Style { get; set; } = null;

    protected string? GetId()
    {
        return string.IsNullOrEmpty(Id) ? null : Id;
    }

}