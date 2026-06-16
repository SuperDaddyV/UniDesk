using CommunityToolkit.Mvvm.ComponentModel;
using UniDesk.Models;
using UniDesk.Services;
using System;
using System.Threading.Tasks;

namespace UniDesk.ViewModels;

public partial class NoteEditViewModel : ObservableObject
{
    private readonly INoteService _noteService;
    private readonly ILocalizationService _localizationService;
    private readonly bool _isNew;
    private readonly int _noteId;
    private readonly DateTime _createdAt;
    private readonly string _color;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    public string WindowTitle => _isNew ? L("QuickNote.New") : L("QuickNote.Edit");

    public NoteEditViewModel(
        INoteService noteService,
        ILocalizationService localizationService,
        NoteItem? note = null)
    {
        _noteService = noteService;
        _localizationService = localizationService;

        if (note == null)
        {
            _isNew = true;
            _noteId = 0;
            _createdAt = DateTime.UtcNow;
            _color = "#FFFFFF";
            return;
        }

        _isNew = false;
        _noteId = note.Id;
        _createdAt = note.CreatedAt == default ? DateTime.UtcNow : note.CreatedAt;
        _color = string.IsNullOrWhiteSpace(note.Color) ? "#FFFFFF" : note.Color;
        Title = note.Title;
        Content = note.Content;
    }

    public async Task<bool> SaveAsync()
    {
        try
        {
            var now = DateTime.UtcNow;

            if (_isNew)
            {
                var note = new NoteItem
                {
                    Title = Title ?? string.Empty,
                    Content = Content ?? string.Empty,
                    Color = _color,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                var id = await _noteService.CreateNoteAsync(note);
                return id > 0;
            }

            var updated = new NoteItem
            {
                Id = _noteId,
                Title = Title ?? string.Empty,
                Content = Content ?? string.Empty,
                Color = _color,
                CreatedAt = _createdAt,
                UpdatedAt = now
            };

            await _noteService.UpdateNoteAsync(updated);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string L(string key) => _localizationService.GetString(key);
}
