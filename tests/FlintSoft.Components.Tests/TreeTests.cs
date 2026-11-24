using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace FlintSoft.Components.Tests;

public sealed class TreeTests : BunitContext
{
    public TreeTests()
    {
        var module = JSInterop.SetupModule("./_content/FlintSoft.Components/tree.js");
        module.SetupVoid("attachTree", _ => true).SetVoidResult();
        module.SetupVoid("detachTree", _ => true).SetVoidResult();
        module.SetupVoid("focusTreeItem", _ => true).SetVoidResult();
    }

    private static FsTreeItem<string> Node(string id, params FsTreeItem<string>[] children) =>
        new() { Id = id, Text = id, ShowActions = false, Value = $"value:{id}", Children = children };
    private static string Selector(string id) => $"[data-fs-tree-id='{id}']";

    public static IEnumerable<object?[]> ExpansionPolicies()
    {
        foreach (bool? item in new bool?[] { null, false, true })
        foreach (bool? tree in new bool?[] { null, false, true })
        foreach (var children in new[] { false, true })
        foreach (FsTreeDecision? decision in new FsTreeDecision?[] { null, FsTreeDecision.Default, FsTreeDecision.Allow, FsTreeDecision.Deny })
            yield return [item, tree, children, decision];
    }

    [Theory]
    [MemberData(nameof(ExpansionPolicies))]
    public async Task ExpansionPolicyCanBeOverriddenBeforeConfigRejects(bool? itemPolicy, bool? treePolicy, bool children, FsTreeDecision? decision)
    {
        var root = Node("root", children ? [Node("child")] : []);
        root.IsExpandable = itemPolicy;
        var requested = 0;
        var completed = 0;
        var tree = Render<FsTree<string>>(p =>
        {
            p.Add(c => c.Items, [root]).Add(c => c.DefaultIsExpandable, treePolicy)
                .Add(c => c.ExpandedChanged, args => { Assert.Same(root, args.Item); completed++; });
            if (decision is not null)
                p.Add(c => c.ExpansionRequested, args =>
                {
                    Assert.False(args.IsExpanded);
                    Assert.True(args.RequestedExpanded);
                    Assert.Equal("root", args.Id);
                    Assert.Null(args.ParentId);
                    requested++;
                    args.Decision = decision.Value;
                });
        });
        var configured = itemPolicy ?? treePolicy ?? children;
        var expected = decision == FsTreeDecision.Allow || (decision != FsTreeDecision.Deny && configured);
        var button = tree.FindAll(".fs-tree-toggle").FirstOrDefault();
        Assert.Equal(children, button is not null);
        if (button is not null) button.Click();
        else await tree.InvokeAsync(() => tree.Instance.TreeKeyAsync("root", "ArrowRight"));
        var semanticBranch = children || configured || decision is not null || expected;
        Assert.Equal(expected ? "true" : semanticBranch ? "false" : null, tree.Find(Selector("root")).GetAttribute("aria-expanded"));
        Assert.Equal(decision is null ? 0 : 1, requested);
        Assert.Equal(expected ? 1 : 0, completed);
        Assert.Equal(expected && children ? 1 : 0, tree.FindAll(Selector("child")).Count);
        Assert.Equal(itemPolicy, root.IsExpandable);
    }

    public static IEnumerable<object?[]> ClickPolicies()
    {
        foreach (bool? item in new bool?[] { null, false, true })
        foreach (var tree in new[] { false, true })
        foreach (FsTreeDecision? decision in new FsTreeDecision?[] { null, FsTreeDecision.Default, FsTreeDecision.Allow, FsTreeDecision.Deny })
            yield return [item, tree, decision];
    }

    [Theory]
    [MemberData(nameof(ClickPolicies))]
    public void ClickPolicyExposesPayloadAndOverridesConfig(bool? itemPolicy, bool treePolicy, FsTreeDecision? decision)
    {
        var root = Node("root");
        root.IsClickable = itemPolicy;
        var calls = new List<string>();
        var tree = Render<FsTree<string>>(p =>
        {
            p.Add(c => c.Items, [root]).Add(c => c.DefaultIsClickable, treePolicy)
                .Add(c => c.SelectedIdChanged, id => { Assert.Equal("root", id); calls.Add("selection"); })
                .Add(c => c.ItemClicked, args =>
                {
                    Assert.Same(root, args.Item);
                    Assert.Equal("value:root", args.Item.Value);
                    Assert.Equal("root", args.SelectedId);
                    calls.Add("clicked");
                });
            if (decision is not null)
                p.Add(c => c.ItemClickRequested, args => { calls.Add("request"); args.Decision = decision.Value; });
        });
        tree.Find(".fs-tree-label").Click();
        var allowed = decision == FsTreeDecision.Allow || (decision != FsTreeDecision.Deny && (itemPolicy ?? treePolicy));
        Assert.Equal(allowed ? "true" : "false", tree.Find(Selector("root")).GetAttribute("aria-selected"));
        var expected = new List<string>();
        if (decision is not null) expected.Add("request");
        if (allowed) expected.AddRange(["selection", "clicked"]);
        Assert.Equal(expected, calls);
        Assert.Equal(itemPolicy, root.IsClickable);
    }

    [Fact]
    public void CollapseRemainsPossibleAfterPolicyChangesAndCanBeVetoed()
    {
        var root = Node("root", Node("child"));
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [root]));
        tree.Find(".fs-tree-toggle").Click();
        root.IsExpandable = false;
        tree.Render(p => p.Add(c => c.ExpansionRequested, args => args.Decision = FsTreeDecision.Deny));
        tree.Find(".fs-tree-toggle").Click();
        Assert.Equal("true", tree.Find(Selector("root")).GetAttribute("aria-expanded"));
        tree.Render(p => p.Add(c => c.ExpansionRequested, args => args.Decision = FsTreeDecision.Default));
        tree.Find(".fs-tree-toggle").Click();
        Assert.Equal("false", tree.Find(Selector("root")).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void RepeatedActivationNotifiesWithoutSelectingAgainAndDisclosureDoesNotActivate()
    {
        var clicked = 0;
        var selected = 0;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [Node("root", Node("child"))])
            .Add(c => c.ItemClicked, _ => clicked++).Add(c => c.SelectedIdChanged, _ => selected++));
        tree.Find(".fs-tree-toggle").Click();
        Assert.Equal(0, clicked);
        tree.Find(".fs-tree-label").Click();
        tree.Find(".fs-tree-label").Click();
        Assert.Equal(2, clicked);
        Assert.Equal(1, selected);
    }

    [Fact]
    public void DisabledTreeDoesNotStartCallbacksOrExposeDraggableRows()
    {
        var root = Node("root", Node("child"));
        root.IsDraggable = true;
        var calls = 0;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [root]).Add(c => c.Disabled, true)
            .Add(c => c.ExpansionRequested, _ => calls++).Add(c => c.ItemClickRequested, _ => calls++)
            .Add(c => c.ItemDropped, _ => calls++));
        tree.Find(".fs-tree-label").Click();
        Assert.Equal(0, calls);
        Assert.True(tree.Find(".fs-tree-toggle").HasAttribute("disabled"));
        Assert.Equal("false", tree.Find(".fs-tree-row").GetAttribute("draggable"));
        Assert.Equal("-1", tree.Find(Selector("root")).GetAttribute("tabindex"));
    }

    [Fact]
    public void RejectsInvalidGraphs()
    {
        void Reject(params FsTreeItem<string>[] nodes) => Assert.Throws<ArgumentException>(() => Render<FsTree<string>>(p => p.Add(c => c.Items, nodes)));
        Reject(Node(""));
        Reject(Node("same"), Node("same"));
        var cycle = Node("cycle");
        cycle.Children = [cycle];
        Reject(cycle);
        var shared = Node("shared");
        Reject(Node("a", shared), Node("b", shared));
    }

    [Fact]
    public void TemplateEncodingAttributesInstanceIdsAndEmptyContentWork()
    {
        var item = Node("<unsafe>");
        var first = Render<FsTree<string>>(p => p.Add(c => c.Items, [item]).Add(c => c.Class, "custom")
            .Add(c => c.AccessibleLabel, "Departments").AddUnmatched("id", "departments"));
        var second = Render<FsTree<string>>(p => p.Add(c => c.Items, [item]));
        Assert.Equal("<unsafe>", first.Find(".fs-tree-label").TextContent);
        Assert.Empty(first.FindAll("unsafe"));
        Assert.Contains("custom", first.Find("#departments").ClassList);
        Assert.Equal("Departments", first.Find("[role=tree]").GetAttribute("aria-label"));
        Assert.NotEqual(first.Find("li").Id, second.Find("li").Id);
        first.Render(p => p.Add(c => c.ItemTemplate, node => builder => builder.AddContent(0, node.Value)));
        Assert.Equal("value:<unsafe>", first.Find(".fs-tree-label").TextContent);
        first.Render(p => p.Add(c => c.Items, []).Add(c => c.EmptyContent, "Nothing here"));
        Assert.Equal("Nothing here", first.Find(".fs-tree-empty").TextContent);
    }

    [Fact]
    public void ReplacementPreservesStateAndRemovalPrunesIt()
    {
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [Node("a", Node("b"))]));
        tree.Find(".fs-tree-toggle").Click();
        tree.Find(Selector("b") + " .fs-tree-label").Click();
        tree.Render(p => p.Add(c => c.Items, [Node("a", Node("b"))]));
        Assert.Equal("true", tree.Find(Selector("a")).GetAttribute("aria-expanded"));
        Assert.Equal("true", tree.Find(Selector("b")).GetAttribute("aria-selected"));
        tree.Render(p => p.Add(c => c.Items, []));
        tree.Render(p => p.Add(c => c.Items, [Node("a", Node("b"))]));
        Assert.Equal("false", tree.Find(Selector("a")).GetAttribute("aria-expanded"));
        Assert.Equal("false", tree.Find(Selector("a")).GetAttribute("aria-selected"));
    }

    [Fact]
    public void ControlledStateDoesNotMutateParametersOrAnnounceRejectedExpansion()
    {
        var ids = new HashSet<string>();
        IReadOnlyCollection<string>? proposed = null;
        var expanded = 0;
        FsTreeItemClickedEventArgs<string>? clicked = null;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [Node("a", Node("b"))])
            .Add(c => c.ExpandedIds, ids).Add(c => c.ExpandedIdsChanged, value => proposed = value)
            .Add(c => c.ExpandedChanged, _ => expanded++).Add(c => c.SelectedId, (string?)null)
            .Add(c => c.ItemClicked, args => clicked = args));
        tree.Find(".fs-tree-toggle").Click();
        Assert.Equal(["a"], proposed);
        Assert.Empty(ids);
        Assert.Empty(tree.FindAll(Selector("b")));
        Assert.Equal(0, expanded);
        tree.Find(".fs-tree-label").Click();
        Assert.NotNull(clicked);
        Assert.Null(clicked.SelectedId);
        Assert.Equal("false", tree.Find("li").GetAttribute("aria-selected"));
        tree.Render(p => p.Add(c => c.ExpandedIds, new[] { "a", "stale" }));
        Assert.Single(tree.FindAll(Selector("b")));
        Assert.Equal(0, expanded);
    }

    [Fact]
    public void ControlledExpansionAnnouncesOnlyAfterParentAppliesState()
    {
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [Node("a", Node("b"))])
            .Add(c => c.ExpandedIds, Array.Empty<string>()));
        var events = new List<string>();
        tree.Render(p => p.Add(c => c.ExpandedIdsChanged, ids =>
        {
            events.Add("binding");
            tree.Render(update => update.Add(c => c.ExpandedIds, ids));
        }).Add(c => c.ExpandedChanged, args =>
        {
            Assert.Single(tree.FindAll(Selector("b")));
            Assert.True(args.IsExpanded);
            events.Add("completed");
        }));
        tree.Find(".fs-tree-toggle").Click();
        tree.WaitForAssertion(() => Assert.Equal(new[] { "binding", "completed" }, events));
    }

    [Fact]
    public async Task LazyLoadingUsesUpdatedChildrenAndSuppressesDuplicates()
    {
        var root = Node("a");
        root.IsExpandable = true;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [root]).Add(c => c.ExpansionRequested, async args =>
        {
            requests++;
            await ready.Task;
            args.Item.Children = [Node("loaded")];
        }));
        var work = tree.InvokeAsync(() => tree.Instance.RequestExpansionAsync("a", true));
        tree.WaitForAssertion(() => Assert.Equal("true", tree.Find(Selector("a")).GetAttribute("aria-busy")));
        await tree.InvokeAsync(() => tree.Instance.RequestExpansionAsync("a", true));
        Assert.Equal(1, requests);
        Assert.Equal("false", tree.Find(Selector("a")).GetAttribute("aria-expanded"));
        ready.SetResult();
        await work;
        Assert.Single(tree.FindAll(Selector("loaded")));
        Assert.Equal("false", tree.Find(Selector("a")).GetAttribute("aria-busy"));
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("replace")]
    [InlineData("disable")]
    [InlineData("external")]
    [InlineData("dispose")]
    public async Task StaleExpansionRequestsAreCancelled(string change)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        var completed = 0;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [Node("a", Node("b"))])
            .Add(c => c.ExpandedIds, Array.Empty<string>()).Add(c => c.ExpandedChanged, _ => completed++)
            .Add(c => c.ExpansionRequested, async args => { token = args.CancellationToken; await ready.Task; args.Decision = FsTreeDecision.Allow; }));
        var work = tree.InvokeAsync(() => tree.Instance.RequestExpansionAsync("a", true));
        tree.WaitForAssertion(() => Assert.True(token.CanBeCanceled));
        switch (change)
        {
            case "remove": tree.Render(p => p.Add(c => c.Items, [])); break;
            case "replace": tree.Render(p => p.Add(c => c.Items, [Node("a", Node("new"))])); break;
            case "disable": tree.Render(p => p.Add(c => c.Disabled, true)); break;
            case "external": tree.Render(p => p.Add(c => c.ExpandedIds, new[] { "a" })); break;
            case "dispose": await tree.InvokeAsync(async () => await tree.Instance.DisposeAsync()); break;
        }
        Assert.True(token.IsCancellationRequested);
        ready.SetResult();
        await work;
        Assert.Equal(0, completed);
    }

    [Fact]
    public async Task ExceptionsPropagateAndPendingStateIsReleased()
    {
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [Node("a", Node("b"))])
            .Add(c => c.ExpansionRequested, _ => throw new InvalidOperationException("failed")));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => tree.InvokeAsync(() => tree.Instance.RequestExpansionAsync("a", true)));
        Assert.Equal("failed", error.Message);
        Assert.Equal("false", tree.Find(Selector("a")).GetAttribute("aria-busy"));
        Assert.Equal("false", tree.Find(Selector("a")).GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task KeyboardNavigatesVisibleNodesWithoutSelectingAndUsesPolicyPipeline()
    {
        var a = Node("a", Node("child"));
        a.IsExpandable = false;
        var clicked = 0;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [a, Node("zebra")])
            .Add(c => c.ExpansionRequested, args => args.Decision = FsTreeDecision.Allow)
            .Add(c => c.ItemClicked, _ => clicked++));
        async Task Key(string id, string key) => await tree.InvokeAsync(() => tree.Instance.TreeKeyAsync(id, key));
        await Key("a", "ArrowRight");
        Assert.Single(tree.FindAll(Selector("child")));
        await Key("a", "ArrowRight");
        Assert.Equal("0", tree.Find(Selector("child")).GetAttribute("tabindex"));
        await Key("child", "z");
        Assert.Equal("0", tree.Find(Selector("zebra")).GetAttribute("tabindex"));
        Assert.Equal(0, clicked);
        await Key("zebra", "Enter");
        Assert.Equal(1, clicked);
        await Key("zebra", "Home");
        Assert.Equal("0", tree.Find(Selector("a")).GetAttribute("tabindex"));
        await Key("a", "ArrowLeft");
        Assert.Empty(tree.FindAll(Selector("child")));
    }

    [Fact]
    public async Task DropReportsCompleteContextWithoutMutatingData()
    {
        var child = Node("child");
        child.IsDraggable = true;
        var source = Node("source", child);
        var target = Node("target", Node("existing"));
        target.CanAcceptDrop = true;
        FsTreeDropEventArgs<string>? dropped = null;
        FsTreeItem<string>[] items = [source, target];
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, items).Add(c => c.ItemDropped, args => dropped = args));
        await tree.InvokeAsync(() => tree.Instance.MoveAsync("child", "target"));
        Assert.NotNull(dropped);
        Assert.Same(items, dropped.Items);
        Assert.Same(child, dropped.Item);
        Assert.Equal("value:child", dropped.Item.Value);
        Assert.Same(source, dropped.SourceParent);
        Assert.Same(target, dropped.TargetParent);
        Assert.Equal("source", dropped.SourceParentId);
        Assert.Equal("target", dropped.TargetParentId);
        Assert.Equal(0, dropped.SourceIndex);
        Assert.Equal(1, dropped.TargetIndex);
        Assert.Same(child, Assert.Single(source.Children));
        Assert.Equal("existing", Assert.Single(target.Children).Id);
        Assert.Equal("No move applied.", tree.Find(".fs-tree-status").TextContent);
    }

    [Fact]
    public async Task RootSubtreeDropRendersApplicationUpdateAndPreservesState()
    {
        var source = Node("source", Node("child"));
        source.IsDraggable = true;
        var target = Node("target");
        target.CanAcceptDrop = true;
        IReadOnlyList<FsTreeItem<string>> items = [source, target];
        FsTreeDropEventArgs<string>? dropped = null;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, items));
        tree.Render(p => p.Add(c => c.ItemDropped, args =>
        {
            dropped = args;
            target.Children = [source];
            items = [target];
            tree.Render(update => update.Add(c => c.Items, items));
        }));
        tree.Find(".fs-tree-toggle").Click();
        tree.Find(".fs-tree-label").Click();
        await tree.InvokeAsync(() => tree.Instance.MoveAsync("source", "target"));
        Assert.NotNull(dropped);
        Assert.Null(dropped.SourceParent);
        Assert.Null(dropped.SourceParentId);
        Assert.Equal(0, dropped.SourceIndex);
        tree.Find(".fs-tree-toggle").Click();
        Assert.Equal("true", tree.Find(Selector("source")).GetAttribute("aria-expanded"));
        Assert.Equal("true", tree.Find(Selector("source")).GetAttribute("aria-selected"));
        Assert.Single(tree.FindAll(Selector("child")));
        Assert.Equal("Moved source to target.", tree.Find(".fs-tree-status").TextContent);
    }

    [Fact]
    public async Task InvalidMovesAreRejectedAndPermissionsAreIndependent()
    {
        var child = Node("child");
        child.IsDraggable = true;
        child.CanAcceptDrop = true;
        var a = Node("a", child);
        a.IsDraggable = true;
        a.CanAcceptDrop = true;
        var b = Node("b");
        b.CanAcceptDrop = true;
        b.IsClickable = b.IsExpandable = false;
        var locked = Node("locked");
        var calls = 0;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [a, b, locked]).Add(c => c.ItemDropped, _ => calls++));
        foreach (var (source, target) in new[] { ("a", "a"), ("a", "child"), ("child", "a"), ("locked", "b"), ("a", "locked"), ("missing", "b"), ("a", "missing") })
            await tree.InvokeAsync(() => tree.Instance.MoveAsync(source, target));
        Assert.Equal(0, calls);
        await tree.InvokeAsync(() => tree.Instance.MoveAsync("child", "b"));
        Assert.Equal(1, calls);
        Assert.False(b.IsDraggable);
        Assert.Equal("false", tree.Find(Selector("b") + " .fs-tree-row").GetAttribute("draggable"));
        tree.Render(p => p.Add(c => c.Disabled, true));
        await tree.InvokeAsync(() => tree.Instance.MoveAsync("a", "b"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task DropRequiresAHandlerAndAnOwnedCurrentDragSession()
    {
        var a = Node("a");
        a.IsDraggable = true;
        var b = Node("b");
        b.CanAcceptDrop = true;
        var calls = 0;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [a, b]));
        Assert.Equal("false", tree.Find(".fs-tree-row").GetAttribute("draggable"));
        tree.Render(p => p.Add(c => c.ItemDropped, _ => calls++));
        await tree.InvokeAsync(() => tree.Instance.DropTreeAsync("a", "b", "external"));
        Assert.Equal(0, calls);
        await tree.InvokeAsync(() => Assert.True(tree.Instance.BeginTreeDrag("a", "owned")));
        await tree.InvokeAsync(() => tree.Instance.DropTreeAsync("a", "b", "external"));
        Assert.Equal(0, calls);
        await tree.InvokeAsync(() => Assert.True(tree.Instance.BeginTreeDrag("a", "owned")));
        tree.Render(p => p.Add(c => c.Items, new[] { new FsTreeItem<string> { Id = "a", Text = "replacement", ShowActions = false, IsDraggable = true }, b }));
        await tree.InvokeAsync(() => tree.Instance.DropTreeAsync("a", "b", "owned"));
        Assert.Equal(0, calls);
        await tree.InvokeAsync(() => Assert.True(tree.Instance.BeginTreeDrag("a", "new")));
        await tree.InvokeAsync(() => tree.Instance.DropTreeAsync("a", "b", "new"));
        await tree.InvokeAsync(() => tree.Instance.DropTreeAsync("a", "b", "new"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task PendingDropBlocksDuplicatesAndFailureReleasesIt()
    {
        var a = Node("a");
        a.IsDraggable = true;
        var b = Node("b");
        b.CanAcceptDrop = true;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [a, b]).Add(c => c.ItemDropped, async _ => { calls++; await ready.Task; throw new InvalidOperationException("save failed"); }));
        var work = tree.InvokeAsync(() => tree.Instance.MoveAsync("a", "b"));
        tree.WaitForAssertion(() => Assert.Equal("true", tree.Find(".fs-tree").GetAttribute("data-fs-tree-dropping")));
        await tree.InvokeAsync(() => tree.Instance.MoveAsync("a", "b"));
        Assert.Equal(1, calls);
        ready.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => work);
        Assert.Equal("false", tree.Find(".fs-tree").GetAttribute("data-fs-tree-dropping"));
        Assert.Equal("true", tree.Find(".fs-tree-row").GetAttribute("draggable"));
        Assert.Equal(2, tree.FindAll("[role=tree] > li").Count);
    }

    [Fact]
    public async Task IndependentNodesCanLoadConcurrentlyAndEmptyBranchesHaveCustomContent()
    {
        var a = Node("a");
        var b = Node("b");
        a.IsExpandable = b.IsExpandable = true;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<string>();
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [a, b])
            .Add(c => c.EmptyChildrenContent, "Empty branch")
            .Add(c => c.ExpansionRequested, async args => { requests.Add(args.Id); await ready.Task; }));
        var first = tree.InvokeAsync(() => tree.Instance.RequestExpansionAsync("a", true));
        var second = tree.InvokeAsync(() => tree.Instance.RequestExpansionAsync("b", true));
        tree.WaitForAssertion(() => Assert.Equal(new[] { "a", "b" }, requests));
        Assert.Equal(2, tree.FindAll("li[aria-busy=true]").Count);
        ready.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(2, tree.FindAll("li[aria-expanded=true]").Count);
        Assert.All(tree.FindAll(".fs-tree-empty"), element => Assert.Equal("Empty branch", element.TextContent));
        Assert.Equal(2, tree.FindAll(".fs-tree-empty").Count);
    }

    [Fact]
    public async Task ExternalSelectionUpdateCancelsPendingActivation()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        var completed = 0;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [Node("a"), Node("b")])
            .Add(c => c.SelectedId, (string?)null).Add(c => c.ItemClicked, _ => completed++)
            .Add(c => c.ItemClickRequested, async args => { token = args.CancellationToken; await ready.Task; }));
        var work = tree.InvokeAsync(() => tree.Instance.ActivateAsync("a"));
        tree.WaitForAssertion(() => Assert.True(token.CanBeCanceled));
        tree.Render(p => p.Add(c => c.SelectedId, "b"));
        Assert.True(token.IsCancellationRequested);
        ready.SetResult();
        await work;
        Assert.Equal(0, completed);
        Assert.Equal("true", tree.Find(Selector("b")).GetAttribute("aria-selected"));
        Assert.Equal("false", tree.Find(Selector("a")).GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task DropAcceptsAuthoritativeReloadWithNewObjectsAtTheDestination()
    {
        var source = Node("source");
        source.IsDraggable = true;
        var target = Node("target");
        target.CanAcceptDrop = true;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [source, target]));
        tree.Render(p => p.Add(c => c.ItemDropped, args =>
        {
            tree.Render(update => update.Add(c => c.Items, [Node("target", Node("source"))]));
            Assert.False(args.CancellationToken.IsCancellationRequested);
        }));
        await tree.InvokeAsync(() => tree.Instance.MoveAsync("source", "target"));
        Assert.Equal("Moved source to target.", tree.Find(".fs-tree-status").TextContent);
        tree.Find(".fs-tree-toggle").Click();
        Assert.Single(tree.FindAll(Selector("target") + " " + Selector("source")));
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("replace")]
    [InlineData("disable")]
    [InlineData("permission")]
    [InlineData("dispose")]
    public async Task PendingDropInvalidationCancelsTokenWithoutMutation(string change)
    {
        var source = Node("source");
        source.IsDraggable = true;
        var target = Node("target");
        target.CanAcceptDrop = true;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [source, target])
            .Add(c => c.ItemDropped, async args => { token = args.CancellationToken; await ready.Task; token.ThrowIfCancellationRequested(); }));
        var work = tree.InvokeAsync(() => tree.Instance.MoveAsync("source", "target"));
        tree.WaitForAssertion(() => Assert.True(token.CanBeCanceled));
        switch (change)
        {
            case "remove": tree.Render(p => p.Add(c => c.Items, [target])); break;
            case "replace": tree.Render(p => p.Add(c => c.Items, [Node("source"), target])); break;
            case "disable": tree.Render(p => p.Add(c => c.Disabled, true)); break;
            case "permission": target.CanAcceptDrop = false; tree.Render(); break;
            case "dispose": await tree.InvokeAsync(async () => await tree.Instance.DisposeAsync()); break;
        }
        Assert.True(token.IsCancellationRequested);
        ready.SetResult();
        await work;
        Assert.Empty(target.Children);
        if (change != "dispose") Assert.Equal("false", tree.Find(".fs-tree").GetAttribute("data-fs-tree-dropping"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrowserCallbackErrorsReachAnErrorBoundary(bool drop)
    {
        var source = Node("source");
        source.IsDraggable = true;
        var target = Node("target");
        target.CanAcceptDrop = true;
        var boundary = Render<Microsoft.AspNetCore.Components.Web.ErrorBoundary>(p => p
            .AddChildContent<FsTree<string>>(tree => tree.Add(c => c.Items, [source, target])
                .Add(c => c.ItemClickRequested, _ => throw new InvalidOperationException("activation failed"))
                .Add(c => c.ItemDropped, _ => throw new InvalidOperationException("save failed")))
            .Add(c => c.ErrorContent, error => builder => builder.AddContent(0, error.Message)));
        var tree = boundary.FindComponent<FsTree<string>>();
        await tree.InvokeAsync(async () =>
        {
            if (drop)
            {
                Assert.True(tree.Instance.BeginTreeDrag("source", "session"));
                await tree.Instance.DropTreeAsync("source", "target", "session");
            }
            else await tree.Instance.TreeKeyAsync("source", "Enter");
        });
        boundary.WaitForAssertion(() => Assert.Contains(drop ? "save failed" : "activation failed", boundary.Markup));
        Assert.Empty(boundary.FindAll("[role=tree]"));
    }

    [Fact]
    public void ChevronsChangeDirectionAndDisappearWhenChildrenAreRemoved()
    {
        var root = Node("root", Node("child"));
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [root]));
        Assert.Equal("m9 18 6-6-6-6", tree.Find(".fs-tree-toggle svg path").GetAttribute("d"));
        Assert.Equal("true", tree.Find(".fs-tree-toggle > span").GetAttribute("aria-hidden"));
        tree.Find(".fs-tree-toggle").Click();
        Assert.Equal("m6 9 6 6 6-6", tree.Find(".fs-tree-toggle svg path").GetAttribute("d"));
        Assert.Empty(tree.FindAll(Selector("child") + " .fs-tree-toggle"));
        root.Children = [];
        tree.Render();
        Assert.Empty(tree.FindAll(".fs-tree-toggle"));
        Assert.Single(tree.FindAll(".fs-tree-toggle-space"));
        Assert.Equal("true", tree.Find(Selector("root")).GetAttribute("aria-expanded"));
        root.Children = [Node("replacement")];
        tree.Render();
        Assert.Equal("m6 9 6 6 6-6", tree.Find(".fs-tree-toggle svg path").GetAttribute("d"));
        tree.Find(".fs-tree-toggle").Click();
        Assert.Equal("m9 18 6-6-6-6", tree.Find(".fs-tree-toggle svg path").GetAttribute("d"));
    }

    [Fact]
    public void ActionsReceiveEachVisibleNodeAndDoNotActivateOrSelectRows()
    {
        var child = Node("child");
        child.ShowActions = true;
        var root = Node("root", child);
        root.ShowActions = true;
        FsTreeItem<string>? actionItem = null;
        var activations = 0;
        var expansions = 0;
        var tree = Render<FsTree<string>>(p => p.Add(c => c.Items, [root, Node("hidden")])
            .Add(c => c.ItemClickRequested, _ => activations++)
            .Add(c => c.ExpansionRequested, _ => expansions++)
            .Add(c => c.ExpandedIds, new[] { "root" })
            .Add(c => c.Actions, node => builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, "type", "button");
                builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => actionItem = node));
                builder.AddContent(3, $"Add to {node.Id}");
                builder.CloseElement();
            }));
        Assert.Equal(2, tree.FindAll(".fs-tree-actions").Count);
        Assert.Equal("Actions for child", tree.Find(Selector("child") + " .fs-tree-actions").GetAttribute("aria-label"));
        tree.Find(Selector("child") + " .fs-tree-actions button").Click();
        Assert.Same(child, actionItem);
        Assert.Equal(0, activations);
        Assert.Equal(0, expansions);
        Assert.Equal("false", tree.Find(Selector("root")).GetAttribute("aria-selected"));
        Assert.Equal("false", tree.Find(Selector("child")).GetAttribute("aria-selected"));
        child.ShowActions = false;
        tree.Render();
        Assert.Empty(tree.FindAll(Selector("child") + " .fs-tree-actions"));
        Assert.Single(tree.FindAll(".fs-tree-actions"));
        tree.Render(p => p.Add(c => c.Disabled, true));
        Assert.True(tree.Find(".fs-tree-actions").HasAttribute("disabled"));
        tree.Render(p => p.Add(c => c.Actions, (RenderFragment<FsTreeItem<string>>?)null));
        Assert.Empty(tree.FindAll(".fs-tree-actions"));
    }
}
