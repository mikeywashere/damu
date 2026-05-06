using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DamYou.Extensions
{
    public class RangableObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppress;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppress)
                base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!_suppress)
                base.OnPropertyChanged(e);
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var list = items as IList<T> ?? items.ToList();
            if (list.Count == 0)
                return;

            _suppress = true;
            try
            {
                foreach (var item in list)
                    Items.Add(item);
            }
            finally
            {
                _suppress = false;
            }

            // Fire a single Reset event
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var list = items as IList<T> ?? items.ToList();
            if (list.Count == 0)
                return;

            _suppress = true;
            try
            {
                foreach (var item in list)
                    Items.Remove(item);
            }
            finally
            {
                _suppress = false;
            }

            // Fire a single Reset event
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}