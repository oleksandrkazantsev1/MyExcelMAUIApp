
using Microsoft.Maui;
using MyExcelMAUIApp.Logic;
using MyExcelMAUIApp.Services;

namespace MyExcelMAUIApp
{
    public partial class MainPage : ContentPage
    {
        private const int InitialRows = 20;
        private const int InitialColumns = 10;
        private const double CellWidth = 100;
        private const double HeaderWidth = 40;

        private Spreadsheet _sheet = new(InitialRows, InitialColumns);
        private DisplayMode _mode = DisplayMode.Expressions;
        private Entry[,] _entries = new Entry[0, 0];
        private int _selectedRow = -1;
        private int _selectedColumn = -1;
        private string _currentFileName = "таблиця";

        private readonly ISpreadsheetStorage _localStorage = new LocalFileStorage();
        private readonly ISpreadsheetStorage _driveStorage = new GoogleDriveStorage();

        private const string HelpMessage =
            "Лабораторна робота № 1. Розробка програми для роботи з електронними таблицями.\n" +
            "Виконав: Казанцев Олександр, група К-25. Варіант 38 (набір операцій: 1, 3, 7, 8, 9).\n\n" +
            "Клітинка містить вираз: цілі числа довільної довжини, посилання на клітинки (A1, B12), " +
            "круглі дужки, пробіли та операції:\n" +
            "• бінарні + - * / (ділення цілочисельне);\n" +
            "• унарні + та -;\n" +
            "• mmax(x1, x2, ..., xN), mmin(x1, x2, ..., xN), N >= 1;\n" +
            "• порівняння = < > <= >= <> (результат — ІСТИНА або ХИБНІСТЬ).\n" +
            "Приклад: mmax(A1, B1, 10) >= C1 + 2\n" +
            "Порожня клітинка у виразах дорівнює 0.\n\n" +
            "Режими: «ВИРАЗИ» показує текст виразів, «ЗНАЧЕННЯ» — результати. " +
            "Помилки виділено червоним; текст помилки (з позицією символа) видно під рядком формул.\n" +
            "Обробка двоетапна: 1) синтаксична перевірка, 2) обчислення " +
            "(ділення на нуль, циклічні посилання, невідомі клітинки, невідповідність типів).\n\n" +
            "«Порахувати» — перерахувати таблицю, увімкнути режим значень і показати список помилок.\n" +
            "«Зберегти»/«Прочитати» — файл на локальному диску (Документи\\MyExcelMAUIApp) або на Google Drive.\n" +
            "Кнопки додавання/видалення працюють з останнім рядком або стовпчиком.";

        public MainPage()
        {
            InitializeComponent();
            CreateGrid();
        }

        // ================= Побудова таблиці =================

        private void CreateGrid()
        {
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();
            _entries = new Entry[_sheet.RowCount, _sheet.ColumnCount];
            _selectedRow = _selectedColumn = -1;

            AddColumnsAndColumnLabels();
            AddRowsAndCellEntries();
            UpdateSelectionInfo();
        }

        private void AddColumnsAndColumnLabels()
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(HeaderWidth)));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (int col = 0; col < _sheet.ColumnCount; col++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));
                Label label = CreateHeaderLabel(CellReference.ColumnName(col + 1));
                Grid.SetRow(label, 0);
                Grid.SetColumn(label, col + 1);
                grid.Children.Add(label);
            }
        }

        private void AddRowsAndCellEntries()
        {
            for (int row = 0; row < _sheet.RowCount; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                Label label = CreateHeaderLabel((row + 1).ToString());
                Grid.SetRow(label, row + 1);
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                for (int col = 0; col < _sheet.ColumnCount; col++)
                {
                    Entry entry = CreateEntry(row, col);
                    _entries[row, col] = entry;
                    Grid.SetRow(entry, row + 1);
                    Grid.SetColumn(entry, col + 1);
                    grid.Children.Add(entry);
                }
            }
            RefreshDisplay();
        }

        private static Label CreateHeaderLabel(string text) => new()
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };

        private Entry CreateEntry(int row, int col)
        {
            var entry = new Entry
            {
                Text = _sheet.GetDisplayText(row, col, _mode),
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill
            };
            entry.Focused += Entry_Focused;
            entry.Unfocused += Entry_Unfocused;
            return entry;
        }

        // ================= Відображення =================

        /// <summary>Оновлює текст клітинок згідно з поточним режимом; помилкові клітинки — червоним.</summary>
        private void RefreshDisplay()
        {
            for (int r = 0; r < _sheet.RowCount; r++)
                for (int c = 0; c < _sheet.ColumnCount; c++)
                {
                    Entry entry = _entries[r, c];
                    if (!entry.IsFocused)
                    {
                        string text = _sheet.GetDisplayText(r, c, _mode);
                        if (entry.Text != text) entry.Text = text;
                    }
                    if (_sheet.HasError(r, c)) entry.TextColor = Colors.Red;
                    else entry.ClearValue(Entry.TextColorProperty);
                }
        }

        private void UpdateSelectionInfo()
        {
            if (_selectedRow < 0)
            {
                CellNameLabel.Text = "—";
                textInput.Text = "";
                return;
            }
            CellNameLabel.Text = CellReference.ToName(_selectedRow, _selectedColumn);
            textInput.Text = _sheet.GetExpression(_selectedRow, _selectedColumn);
            StatusLabel.Text = _sheet.DescribeCell(_selectedRow, _selectedColumn);
        }

        private void SetMode(DisplayMode mode)
        {
            _mode = mode;
            ModeButton.Text = mode == DisplayMode.Expressions ? "Режим: ВИРАЗИ" : "Режим: ЗНАЧЕННЯ";
            RefreshDisplay();
        }

        private void CommitCell(int row, int column, string? text)
        {
            if (_sheet.SetExpression(row, column, text ?? "")) _sheet.Recalculate();
            RefreshDisplay();
            if (row == _selectedRow && column == _selectedColumn) UpdateSelectionInfo();
        }

        // ================= Події клітинок =================

        private bool TryGetCoordinates(Entry entry, out int row, out int column)
        {
            row = Grid.GetRow(entry) - 1;
            column = Grid.GetColumn(entry) - 1;
            return row >= 0 && column >= 0 &&
                   row < _entries.GetLength(0) && column < _entries.GetLength(1) &&
                   _entries[row, column] == entry;
        }

        // Користувач увійшов у клітинку: показуємо вираз для редагування
        private void Entry_Focused(object? sender, FocusEventArgs e)
        {
            var entry = (Entry)sender!;
            if (!TryGetCoordinates(entry, out int row, out int column)) return;

            _selectedRow = row;
            _selectedColumn = column;
            string expression = _sheet.GetExpression(row, column);
            if (entry.Text != expression) entry.Text = expression;
            UpdateSelectionInfo();
        }

        // Користувач вийшов із клітинки: зберігаємо вираз і перераховуємо таблицю
        private void Entry_Unfocused(object? sender, FocusEventArgs e)
        {
            var entry = (Entry)sender!;
            if (!TryGetCoordinates(entry, out int row, out int column)) return;
            CommitCell(row, column, entry.Text);
        }

        private void FormulaBar_Completed(object? sender, EventArgs e)
        {
            if (_selectedRow >= 0) CommitCell(_selectedRow, _selectedColumn, textInput.Text);
        }

        // ================= Кнопки =================

        private async void CalculateButton_Clicked(object sender, EventArgs e) => await CalculateAsync();

        private async void SaveButton_Clicked(object sender, EventArgs e) => await SaveAsync();

        private async void ReadButton_Clicked(object sender, EventArgs e) => await ReadAsync();

        private void ModeButton_Clicked(object sender, EventArgs e) =>
            SetMode(_mode == DisplayMode.Expressions ? DisplayMode.Values : DisplayMode.Expressions);

        private async void ExitButton_Clicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlert("Підтвердження", "Ви дійсно хочете вийти?", "Так", "Ні");
            if (answer) Application.Current?.Quit();
        }

        private async void HelpButton_Clicked(object sender, EventArgs e) =>
            await DisplayAlert("Довідка", HelpMessage, "OK");

        private async void DeleteRowButton_Clicked(object sender, EventArgs e) =>
            await ChangeStructureAsync(_sheet.RemoveLastRow, "У таблиці має залишитися хоча б один рядок.");

        private async void DeleteColumnButton_Clicked(object sender, EventArgs e) =>
            await ChangeStructureAsync(_sheet.RemoveLastColumn, "У таблиці має залишитися хоча б один стовпчик.");

        private async void AddRowButton_Clicked(object sender, EventArgs e) =>
            await ChangeStructureAsync(_sheet.AddRow, $"Досягнуто максимум рядків ({Spreadsheet.MaxRows}).");

        private async void AddColumnButton_Clicked(object sender, EventArgs e) =>
            await ChangeStructureAsync(_sheet.AddColumn, $"Досягнуто максимум стовпчиків ({Spreadsheet.MaxColumns}).");

        // ================= Сценарії (UI-оркестрація) =================

        private async Task ChangeStructureAsync(Func<bool> change, string failMessage)
        {
            if (!change())
            {
                await DisplayAlert("Увага", failMessage, "OK");
                return;
            }
            _sheet.Recalculate();
            CreateGrid();
        }

        private async Task CalculateAsync()
        {
            _sheet.Recalculate();
            SetMode(DisplayMode.Values);
            UpdateSelectionInfo();

            IReadOnlyList<CellError> errors = _sheet.GetErrors();
            if (errors.Count == 0)
            {
                StatusLabel.Text = "Обчислення завершено без помилок.";
                return;
            }

            const int limit = 10;
            string list = string.Join("\n", errors.Take(limit).Select(er => $"{er.CellName}: {er.Message}"));
            if (errors.Count > limit) list += $"\n... та ще {errors.Count - limit}";
            await DisplayAlert("Знайдено помилки", $"Кількість помилок: {errors.Count}\n\n{list}", "OK");
        }

        private async Task<ISpreadsheetStorage?> ChooseStorageAsync(string title)
        {
            string choice = await DisplayActionSheet(
                title, "Скасувати", null, _localStorage.DisplayName, _driveStorage.DisplayName);
            if (choice == _localStorage.DisplayName) return _localStorage;
            if (choice == _driveStorage.DisplayName) return _driveStorage;
            return null;
        }

        private async Task SaveAsync()
        {
            ISpreadsheetStorage? storage = await ChooseStorageAsync("Зберегти таблицю");
            if (storage == null) return;

            string? name = await DisplayPromptAsync(
                "Зберегти таблицю", "Назва файлу:", "Зберегти", "Скасувати",
                maxLength: 60, initialValue: _currentFileName);
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                StatusLabel.Text = $"Збереження ({storage.DisplayName})...";
                string json = SpreadsheetSerializer.Serialize(_sheet);
                string location = await storage.SaveAsync(name, json);
                _currentFileName = FileNaming.StripSuffix(name.Trim());
                StatusLabel.Text = $"Збережено: {location}";
                await DisplayAlert("Збережено", location, "OK");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Помилка збереження.";
                await DisplayAlert("Помилка збереження", ex.Message, "OK");
            }
        }

        private async Task ReadAsync()
        {
            ISpreadsheetStorage? storage = await ChooseStorageAsync("Прочитати таблицю");
            if (storage == null) return;

            try
            {
                StatusLabel.Text = $"Пошук файлів ({storage.DisplayName})...";
                IReadOnlyList<StoredFile> files = await storage.ListAsync();
                if (files.Count == 0)
                {
                    StatusLabel.Text = "Файлів не знайдено.";
                    await DisplayAlert("Прочитати", "Збережених таблиць не знайдено.", "OK");
                    return;
                }

                string choice = await DisplayActionSheet(
                    "Оберіть файл", "Скасувати", null, files.Select(f => f.Name).ToArray());
                StoredFile? file = files.FirstOrDefault(f => f.Name == choice);
                if (file == null)
                {
                    StatusLabel.Text = "Читання скасовано.";
                    return;
                }

                string json = await storage.ReadAsync(file);
                _sheet = SpreadsheetSerializer.Deserialize(json);
                _currentFileName = FileNaming.StripSuffix(file.Name);
                CreateGrid();
                StatusLabel.Text = $"Прочитано: {file.Name}";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Помилка читання.";
                await DisplayAlert("Помилка читання", ex.Message, "OK");
            }
        }
    }
}
