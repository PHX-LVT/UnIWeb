namespace AdminSite.Components.Pages.SectionEditors;

public sealed class SectionContentEditSession
{
    private Func<Task>? _save;

    public bool IsDirty { get; private set; }
    public bool IsSaving { get; private set; }
    public bool CanSave => _save is not null;
    public event Action? Changed;

    public void Register(Func<Task> save)
    {
        if (_save == save) return;
        _save = save;
        Changed?.Invoke();
    }

    public void MarkDirty()
    {
        if (IsDirty) return;
        IsDirty = true;
        Changed?.Invoke();
    }

    public void MarkSaved()
    {
        IsDirty = false;
        IsSaving = false;
        Changed?.Invoke();
    }

    public void Reset()
    {
        _save = null;
        IsDirty = false;
        IsSaving = false;
        Changed?.Invoke();
    }

    public async Task SaveAsync()
    {
        if (_save is null || IsSaving || !IsDirty) return;
        IsSaving = true;
        Changed?.Invoke();
        try { await _save(); }
        finally
        {
            IsSaving = false;
            Changed?.Invoke();
        }
    }
}
