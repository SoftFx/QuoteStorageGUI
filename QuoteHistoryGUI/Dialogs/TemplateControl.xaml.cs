using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for TemplateControl.xaml
    /// </summary>
    public partial class TemplateControl : UserControl, INotifyPropertyChanged
    {
        public SelectableItemSource Symbols { get; set; }
        public SelectableItemSource Years { get; set; }
        public SelectableItemSource Months { get; set; }
        public SelectableItemSource Days { get; set; }
        public SelectableItemSource Hours { get; set; }
        public SelectableItemSource Ticks { get; set; }
        public SelectableItemSource Templates { get; set; }
        public SelectableItemSource Mapping { get; set; }
        public String SymbolMapFrom { get; set; }
        public String SymbolMapTo { get; set; }
        public TemplateControl()
        {
            Symbols = new SelectableItemSource();
            Years = new SelectableItemSource();

            Months = new SelectableItemSource(CultureInfo.InvariantCulture.DateTimeFormat.MonthNames.TakeWhile(m => m.Length > 0).Select((m, i) => new SelectableItem((i + 1).ToString(), m)));

            List<string> days = new List<string>(Enumerable.Range(1, 31).Select(h => h.ToString()))
            {
                "H1*", "H1 bid*", "H1 ask*"
            };

            Days = new SelectableItemSource(days);
            List<string> hours = new List<string>(Enumerable.Range(0, 24).Select(h => h.ToString()))
            {
                "M1*", "M1 bid*", "M1 ask*"
            };
            Hours = new SelectableItemSource(hours);
            Ticks = new SelectableItemSource(new[] { "ticks*", "(ticks file|ticks meta)", "ticks level2*" });
            Templates = new SelectableItemSource();
            Mapping = new SelectableItemSource();

            InitializeComponent();

            DataContext = this;
        }

        public void SetData(IEnumerable<string> symbols, IEnumerable<string> years, IEnumerable<string> templates)
        {
            if (symbols != null)
            {
                foreach (string symbol in symbols)
                {
                    if (symbol != "Loading")
                        Symbols.Add(new SelectableItem(symbol));
                }
                //OnPropertyChanged(nameof(Symbols));
            }

            if (years != null)
            {
                foreach (string year in years)
                {
                    Years.Add(new SelectableItem(year));
                }
                //OnPropertyChanged(nameof(Years));
            }

            if (templates != null)
            {
                foreach (string template in templates)
                {
                    Templates.Add(new SelectableItem(template, null, true));
                }
                //OnPropertyChanged(nameof(Templates));
            }
        }

        private string MakeTemplate(bool clear = true)
        {
            string template = "";

            string ticksTemplate = (Ticks.Text == "*") ? "" : "/" + Ticks.Text;
            var star = !string.IsNullOrEmpty(ticksTemplate);
            string hoursTemplate = (!star && Hours.Text == "*") ? "" : "/" + Hours.Text;
            star = !string.IsNullOrEmpty(hoursTemplate);
            string daysTemplate = (!star && Days.Text == "*") ? "" : "/" + Days.Text;
            star = !string.IsNullOrEmpty(daysTemplate);
            string monthsTemplate = (!star && Months.Text == "*") ? "" : "/" + Months.Text;
            star = !string.IsNullOrEmpty(monthsTemplate);
            string yearsTemplate = (!star && Years.Text == "*") ? "" : "/" + Years.Text;
            star = !string.IsNullOrEmpty(yearsTemplate);
            string symbolsTemplate = (!star && Symbols.Text == "*") ? "" : Symbols.Text;

            template += symbolsTemplate;
            template += yearsTemplate;
            template += monthsTemplate;
            template += daysTemplate;
            template += hoursTemplate;
            template += ticksTemplate;

            if (clear)
            {
                Symbols.ClearSelections();
                Years.ClearSelections();
                Months.ClearSelections();
                Days.ClearSelections();
                Hours.ClearSelections();
                Ticks.ClearSelections();
            }

            return template;
        }

        private void AddButtonClick(object sender, RoutedEventArgs e)
        {
            string template = MakeTemplate();
            if (!string.IsNullOrEmpty(template))
                Templates.Add(new SelectableItem(template, null, true));
            else Templates.Add(new SelectableItem("*", null, true));
        }


        private void AddMappingButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(String.IsNullOrEmpty(SymbolMapFrom) && String.IsNullOrEmpty(SymbolMapFrom))){
                Mapping.Add(new SelectableItem(SymbolMapFrom + " -> " + SymbolMapTo, null, true));
            }
        }

        private void RemoveButtonClick(object sender, RoutedEventArgs e)
        {
            SelectableItem item = (sender as Button)?.CommandParameter as SelectableItem;
            if (item == null)
                return;
            Templates.Remove(item);
            Mapping.Remove(item);
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class SelectableItem : INotifyPropertyChanged
    {
        private string _value;
        public string Value
        {
            get { return _value; }
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        public string Description { get; private set; }
        public string Text => ToString();

        private bool _isChecked;
        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                _isChecked = value;
                OnPropertyChanged();
            }
        }

        public SelectableItem(string value, string description = null, bool isChecked = false)
        {
            Value = value;
            Description = description;
            IsChecked = isChecked;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Description) ? Value : $"{Value} {Description}";
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class SelectableItemSource : INotifyPropertyChanged
    {
        private readonly ObservableCollection<SelectableItem> _source = new ObservableCollection<SelectableItem>();
        private readonly HashSet<SelectableItem> _checkedItems = new HashSet<SelectableItem>();
        private string _text = "*";

        public IEnumerable<SelectableItem> Source => _source;

        public string Text
        {
            get { return _text; }
            set
            {
                UpdateText();
            }
        }

        public SelectableItemSource()
        {
            _source.CollectionChanged += SourceCollectionChanged;
        }

        public SelectableItemSource(IEnumerable<string> source) : this()
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            foreach (string str in source)
            {
                _source.Add(new SelectableItem(str));
            }
        }

        public SelectableItemSource(IEnumerable<SelectableItem> source) : this()
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            foreach (var item in source)
            {
                _source.Add(item);
            }
        }

        public void Add(SelectableItem item)
        {
            _source.Add(item);
        }

        public void Remove(SelectableItem item)
        {
            _source.Remove(item);
        }

        public void ClearSelections()
        {
            _checkedItems.Clear();
            foreach (var item in _source)
            {
                item.IsChecked = false;
            }
            UpdateText();
        }

        private void SourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (SelectableItem item in e.OldItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    _checkedItems.Remove(item);
                }
            }
            if (e.NewItems != null)
            {
                foreach (SelectableItem item in e.NewItems)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                    if (item.IsChecked) _checkedItems.Add(item);
                }
            }
            UpdateText();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsChecked")
            {
                SelectableItem selectableItem = (SelectableItem)sender;
                if (selectableItem.IsChecked)
                {
                    _checkedItems.Add(selectableItem);
                }
                else
                {
                    _checkedItems.Remove(selectableItem);
                }
                UpdateText();
            }
        }

        private void UpdateText()
        {
            switch (_checkedItems.Count)
            {
                case 0:
                    _text = "*";
                    break;
                case 1:
                    _text = _checkedItems.First().Value;
                    break;
                default:
                    _text = $"({string.Join("|", _checkedItems.Select(i => i.Value))})";
                    break;
            }
            OnPropertyChanged(nameof(Text));
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
