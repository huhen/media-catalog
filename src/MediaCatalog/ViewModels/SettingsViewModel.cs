using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MediaCatalog.Helper;
using MediaCatalog.Messages;
using MediaCatalog.Models;
using MediaCatalog.Properties;
using MediaCatalog.Services;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Dapper;
using MediaCatalog.Shared.Controls.Dialogs;
using MediaCatalog.Shared.Services;

namespace MediaCatalog.ViewModels;

/// <summary>
/// ViewModel responsible for managing application settings and data operations.
/// Handles theme switching, data import/export, and database management.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDialogParticipant
{
    private readonly YandexMusicService _yandexMusicService;

    /// <summary>
    /// Gets the application settings instance for binding to UI controls.
    /// </summary>
    public Settings Settings => Settings.Default;

    /// <summary>
    /// Gets whether the user is authenticated with Yandex Music.
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(Settings.YandexMusicToken);

    /// <summary>
    /// Gets the authentication status text for display.
    /// </summary>
    public string AuthStatusText => IsAuthenticated
        ? $"Авторизован: {Settings.YandexMusicUsername}"
        : "Не авторизован";

    /// <summary>
    /// Array of available theme variants that users can select from.
    /// Includes Default, Dark, and Light theme options.
    /// </summary>
    public string[] AvailableThemeVariants { get; } =
    [
        ThemeVariant.Default.ToString(),
        ThemeVariant.Dark.ToString(),
        ThemeVariant.Light.ToString()
    ];

    public SettingsViewModel() : this(new YandexMusicService())
    {
    }

    public SettingsViewModel(YandexMusicService yandexMusicService)
    {
        _yandexMusicService = yandexMusicService;
    }

    /// <summary>
    /// A command that will export the entire database as Json-File
    /// </summary>
    [RelayCommand]
    private async Task ExportDataAsync()
    {
        try
        {
            // Show the file save dialog for the user to choose an export location
            var safeFilePickerResult = await this.SafeFileDialogAsync(
                "Export Data",
                [FileHelper.JsonFileType]);

            if (safeFilePickerResult?.File is { } storageFile)
            {
                // Open file stream for writing
                await using var fs = await storageFile.OpenWriteAsync();

                try
                {
                    // Export all database data to JSON format
                    await DatabaseHelper.ExportToJsonAsync(fs);
                }
                catch (Exception e)
                {
                    // Show an error dialog if export fails
                    await this.ShowOverlayDialogAsync<DialogResult>(
                        "Error",
                        "An error occured during exporting data. " + e.Message,
                        DialogCommands.OkOnly);
                }

                // Show a success dialog with the filename
                await this.ShowOverlayDialogAsync<DialogResult>(
                    "Exported Data",
                    $"Successfully exported data to '{storageFile.Name}'.",
                    DialogCommands.Ok);
            }
        }
        catch (Exception e)
        {
            await this.ShowOverlayDialogAsync<DialogResult>("Could not export the data",
                e.Message, DialogCommands.OkOnly);
        }
    }

    /// <summary>
    /// A command that will import the selected JSON file into the current database.
    /// Existing items will be updated. 
    /// </summary>
    [RelayCommand]
    private async Task ImportDataAsync()
    {
        try
        {
            // NOTE: Existing items will be updated / overridden. You may want to let the user choose 
            // how to handle it. 

            // Show file open dialog for user to select JSON import file
            var openFilePickerResult = await this.OpenFileDialogAsync(
                "Import Data",
                [FileHelper.JsonFileType]);

            if (openFilePickerResult?.FirstOrDefault() is { } storageFile)
            {
                // Open a file stream for reading
                await using var fs = await storageFile.OpenReadAsync();

                // Deserialize JSON into database DTO structure
                var dto = await JsonSerializer.DeserializeAsync<DatabaseDto>(fs,
                    JsonContextHelper.Default.DatabaseDto);

                if (dto is null)
                {
                    throw new FileLoadException("Could not load data");
                }

                // Save all categories from imported data (updates existing ones)
                foreach (var category in dto.Categories ?? [])
                {
                    await category.SaveAsync();
                }

                // Save all ToDoItems from imported data (updates existing ones)
                foreach (var toDoItem in dto.ToDoItems ?? [])
                {
                    await toDoItem.SaveAsync();
                }

                // Notify other ViewModels about updated DB to refresh their views
                WeakReferenceMessenger.Default.Send(new UpdateDataMessage<ToDoItem>(UpdateAction.Reset));
                WeakReferenceMessenger.Default.Send(new UpdateDataMessage<Category>(UpdateAction.Reset));
            }
        }
        catch (Exception e)
        {
            await this.ShowOverlayDialogAsync<DialogResult>("Error importing JSON file",
                e.Message, DialogCommands.OkOnly);
        }
    }

    /// <summary>
    /// A command that clears all data from the database.
    /// Shows a confirmation dialog first, then drops, and recreates all tables.
    /// Notifies other ViewModels to refresh their data after completion.
    /// </summary>
    [RelayCommand]
    private async Task ClearDatabaseAsync()
    {
        // Show a confirmation dialog with a warning about data loss
        var choice = await this.ShowOverlayDialogAsync<DialogResult>(
            "Clear Database",
            """
            Are you sure you want to clear the database? This cannot be undone.
            TIP: Consider to export the data before you continue.

            Press "Yes" to continue.
            """,
            DialogCommands.YesNo);

        if (choice == DialogResult.Yes)
        {
            // Get database connection and clear all data
            await using var connection = await DatabaseHelper.GetOpenConnectionAsync();

            // Drop existing tables and vacuum the database to reclaim space
            await connection.ExecuteAsync(
                """
                DROP TABLE IF EXISTS Category;
                DROP TABLE IF EXISTS ToDoItem;

                VACUUM;
                """);

            // Recreate the database schema
            await DatabaseHelper.EnsureInitializedAsync(connection, true);

            // Notify other ViewModels about updated DB to refresh their views
            WeakReferenceMessenger.Default.Send(new UpdateDataMessage<ToDoItem>(UpdateAction.Reset));
            WeakReferenceMessenger.Default.Send(new UpdateDataMessage<Category>(UpdateAction.Reset));
        }
    }

    [RelayCommand]
    private async Task YandexAuthAsync()
    {
        var userNameTextBox = new Avalonia.Controls.TextBox
        {
            Watermark = "Логин (номер телефона или email)"
        };
        
        var userNameInputPanel = new Avalonia.Controls.StackPanel
        {
            Spacing = 10,
            Children =
            {
                new Avalonia.Controls.TextBlock { Text = "Введите ваш логин:" },
                userNameTextBox
            }
        };
        
        await this.ShowOverlayDialogAsync<string>(
            "Авторизация в Яндекс.Музыка",
            userNameInputPanel,
            DialogCommands.OkCancel);

        var userName = userNameTextBox.Text;

        if (string.IsNullOrWhiteSpace(userName))
            return;

        var authResult = await _yandexMusicService.CreateAuthSessionAsync(userName);

        if (authResult.IsFailure)
        {
            await this.ShowOverlayDialogAsync<DialogResult>(
                "Ошибка",
                authResult.Error,
                DialogCommands.Ok);
            return;
        }

        var methods = authResult.Value.AvailableMethods.ToList();
        
        var selectedMethodHolder = new SelectedMethodHolder();
        
        var methodSelectionPanel = new Avalonia.Controls.StackPanel
        {
            Spacing = 10
        };
        
        foreach (var method in methods)
        {
            var methodCopy = method;
            var radioButton = new Avalonia.Controls.RadioButton
            {
                Content = method.GetDisplayName(),
                GroupName = "AuthMethod",
                Tag = methodCopy
            };
            
            radioButton.Checked += (s, e) =>
            {
                selectedMethodHolder.SelectedMethod = methodCopy;
            };
            
            methodSelectionPanel.Children.Add(radioButton);
        }

        var selectedMethod = await this.ShowOverlayDialogAsync<YandexAuthMethod>(
            "Выберите способ авторизации",
            methodSelectionPanel,
            DialogCommands.OkCancel);

        if (selectedMethod == default(YandexAuthMethod) && selectedMethodHolder.SelectedMethod != default(YandexAuthMethod))
        {
            selectedMethod = selectedMethodHolder.SelectedMethod;
        }

        if (selectedMethod == default(YandexAuthMethod))
            return;

        await this.ShowOverlayDialogAsync<DialogResult>(
            "Выбран способ",
            $"Вы выбрали: {selectedMethod.GetDisplayName()}",
            DialogCommands.Ok);
    }

    private class SelectedMethodHolder
    {
        public YandexAuthMethod SelectedMethod { get; set; }
    }

    [RelayCommand]
    private async Task YandexQrAuthAsync()
    {
        var qrViewModel = new YandexQrAuthViewModel(_yandexMusicService);
        
        _ = qrViewModel.StartAuthAsync();

        var view = new MediaCatalog.Views.YandexQrAuthView
        {
            DataContext = qrViewModel
        };

        var result = await this.ShowOverlayDialogAsync<DialogResult>(
            "QR-авторизация",
            view,
            DialogCommands.Cancel);

        if (qrViewModel.WasSuccessful)
        {
            Settings.YandexMusicUsername = qrViewModel.UserDisplayName;

            var tokenResult = await _yandexMusicService.GetAccessTokenAsync();
            if (tokenResult.IsSuccess)
            {
                Settings.YandexMusicToken = tokenResult.Value.AccessToken;
            }

            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(AuthStatusText));

            await this.ShowOverlayDialogAsync<DialogResult>(
                "Добро пожаловать!",
                $"Вы успешно авторизовались как {qrViewModel.UserDisplayName}!",
                DialogCommands.Ok);
        }
    }
}