using ListForge.Models;

namespace ListForge.ViewModels;

public sealed class ComparisonViewModel : INotifyPropertyChanged
{
    private ComparisonFilterOption? _selectedFilter;
    private ComparisonItem? _selectedItem;

    public ComparisonViewModel(ListComparisonResult result, double editorFontSize)
    {
        Result = result;
        EditorFontSize = Math.Clamp(editorFontSize, 8, 32);
        BuildFilters();
        _selectedFilter = Filters.FirstOrDefault();
        ApplyFilter();

        NextDifferenceCommand = new RelayCommand(() => NavigateDifference(1), () => Result.Items.Any(item => item.RequiresReview));
        PreviousDifferenceCommand = new RelayCommand(() => NavigateDifference(-1), () => Result.Items.Any(item => item.RequiresReview));
        SelectedItem = FilteredItems.FirstOrDefault(item => item.RequiresReview) ?? FilteredItems.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<ComparisonItem?>? SelectionRequested;

    public ListComparisonResult Result { get; }
    public ComparisonSummary Summary => Result.Summary;
    public string InputText => Result.Snapshot.InputText;
    public string OutputText => Result.Snapshot.OutputText;
    public double EditorFontSize { get; }
    public string ActiveWorkProfileName => Result.Snapshot.ActiveWorkProfileName;
    public string SortModeDisplay => Result.Snapshot.SortMode switch
    {
        Core.ListSortMode.Ascending => "Crescente",
        Core.ListSortMode.Descending => "Decrescente",
        _ => "Original",
    };
    public string AdvancedListDisplay => Result.Snapshot.AdvancedListEnabled ? "Avançada" : "Básica";
    public string OutputOriginDisplay => Result.Snapshot.OutputWasManuallyEdited
        ? "Saída com edição manual aplicada"
        : "Saída gerada pelo processamento";
    public string PrivacyNotice => "O relatório pode conter dados da lista. Compartilhe-o somente quando necessário.";

    public ObservableCollection<ComparisonFilterOption> Filters { get; } = [];
    public ObservableCollection<ComparisonItem> FilteredItems { get; } = [];
    public ObservableCollection<ComparisonFieldDifference> SelectedFieldDifferences { get; } = [];

    public ICommand NextDifferenceCommand { get; }
    public ICommand PreviousDifferenceCommand { get; }

    public ComparisonFilterOption? SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (Equals(_selectedFilter, value)) return;
            _selectedFilter = value;
            Notify();
            ApplyFilter();
        }
    }

    public ComparisonItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (Equals(_selectedItem, value)) return;
            _selectedItem = value;
            Notify();
            RefreshSelectedDifferences();
            SelectionRequested?.Invoke(value);
        }
    }

    public bool HasSelectedItem => SelectedItem != null;
    public bool HasSelectedFieldDifferences => SelectedFieldDifferences.Count > 0;
    public string SelectedDetailsTitle => SelectedItem == null
        ? "Selecione um registro para ver os detalhes."
        : $"{SelectedItem.CategoryLabel} - entrada {SelectedItem.InputLineDisplay}, saída {SelectedItem.OutputLineDisplay}";
    public string SelectedDetailsText => SelectedItem?.Details ?? "";
    public string FilterStatusText => $"Exibindo {FilteredItems.Count} de {Result.Items.Count} registro(s) comparado(s).";

    public string BuildClipboardReport()
    {
        var summary = Summary;
        var lines = new List<string>
        {
            "Comparação entre entrada e saída",
            PrivacyNotice,
            "",
            $"Entrada: {summary.InputRecords} registro(s)",
            $"Saída: {summary.OutputRecords} registro(s)",
            $"Correspondentes: {summary.Matching}",
            $"Apenas reorganizados: {summary.Reordered}",
            $"Transformados: {summary.Transformed}",
            $"Alterados: {summary.Changed}",
            $"Possivelmente ausentes: {summary.PossiblyMissing}",
            $"Adicionados: {summary.Added}",
            $"Correspondências incertas: {summary.Uncertain}",
            $"Duplicidades na entrada: {summary.InputDuplicates}",
            $"Duplicidades na saída: {summary.OutputDuplicates}",
            "",
            summary.StatusMessage,
        };

        var reviewItems = Result.Items.Where(item => item.RequiresReview).ToList();
        if (reviewItems.Count > 0)
        {
            lines.Add("");
            lines.Add("Diferenças para revisão:");
            foreach (var item in reviewItems)
            {
                lines.Add($"- {item.CategoryLabel} | entrada {item.InputLineDisplay} | saída {item.OutputLineDisplay} | {item.Summary}");
                foreach (var difference in item.FieldDifferences)
                {
                    var reason = string.IsNullOrWhiteSpace(difference.Reason) ? "" : $" | {difference.Reason}";
                    lines.Add($"  {difference.FieldName}: '{difference.InputValue}' -> '{difference.OutputValue}'{reason}");
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void BuildFilters()
    {
        Filters.Clear();
        Filters.Add(new ComparisonFilterOption(null, "Todos", Result.Items.Count));
        AddFilter(ComparisonCategory.Matching, "Correspondentes");
        AddFilter(ComparisonCategory.Reordered, "Reorganizados");
        AddFilter(ComparisonCategory.Transformed, "Transformados");
        AddFilter(ComparisonCategory.Changed, "Alterados");
        AddFilter(ComparisonCategory.PossiblyMissing, "Possivelmente ausentes");
        AddFilter(ComparisonCategory.Added, "Adicionados");
        AddFilter(ComparisonCategory.Uncertain, "Correspondência incerta");
    }

    private void AddFilter(ComparisonCategory category, string label) =>
        Filters.Add(new ComparisonFilterOption(category, label, Result.Items.Count(item => item.Category == category)));

    private void ApplyFilter()
    {
        var selectedCategory = SelectedFilter?.Category;
        var items = selectedCategory == null
            ? Result.Items
            : Result.Items.Where(item => item.Category == selectedCategory).ToList();

        FilteredItems.Clear();
        foreach (var item in items)
            FilteredItems.Add(item);

        if (SelectedItem == null || !FilteredItems.Contains(SelectedItem))
            SelectedItem = FilteredItems.FirstOrDefault(item => item.RequiresReview) ?? FilteredItems.FirstOrDefault();

        Notify(nameof(FilterStatusText));
    }

    private void NavigateDifference(int direction)
    {
        var problems = Result.Items.Where(item => item.RequiresReview).ToList();
        if (problems.Count == 0)
            return;

        var currentIndex = SelectedItem == null ? -1 : problems.IndexOf(SelectedItem);
        var nextIndex = currentIndex < 0
            ? direction > 0 ? 0 : problems.Count - 1
            : (currentIndex + direction + problems.Count) % problems.Count;
        var target = problems[nextIndex];

        if (!FilteredItems.Contains(target))
            SelectedFilter = Filters.First(filter => filter.Category == null);

        SelectedItem = target;
    }

    private void RefreshSelectedDifferences()
    {
        SelectedFieldDifferences.Clear();
        if (SelectedItem != null)
        {
            foreach (var difference in SelectedItem.FieldDifferences)
                SelectedFieldDifferences.Add(difference);
        }

        Notify(nameof(HasSelectedItem));
        Notify(nameof(HasSelectedFieldDifferences));
        Notify(nameof(SelectedDetailsTitle));
        Notify(nameof(SelectedDetailsText));
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
