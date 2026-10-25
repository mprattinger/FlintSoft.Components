namespace Demo.Components.Pages;

public sealed record ProjectEditorRequest(int? Id, string Name, string Summary);

public sealed record ProjectEditorResult(int? Id, string Name, string Summary);
