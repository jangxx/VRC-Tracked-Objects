using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRC_OSC_ExternallyTrackedObject
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private Dictionary<string, PropertyChangedEventHandler> _propertyChangeHandlers = new Dictionary<string, PropertyChangedEventHandler>();

        protected void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void HandleObervableChildrenChanged(string propertyName, NotifyCollectionChangedEventArgs e)
        {
            if (!this._propertyChangeHandlers.TryGetValue(propertyName, out var handler))
            {
                handler = (object? sender, PropertyChangedEventArgs e) => RaisePropertyChanged(propertyName);
                this._propertyChangeHandlers[propertyName] = handler;
            }

            if (e.NewItems != null)
            {
                foreach (INotifyPropertyChanged item in e.NewItems.OfType<INotifyPropertyChanged>())
                {
                    item.PropertyChanged += handler;
                }
            }

            if (e.OldItems != null)
            {
                foreach (INotifyPropertyChanged item in e.OldItems.OfType<INotifyPropertyChanged>())
                {
                    item.PropertyChanged -= handler;
                }
            }

            RaisePropertyChanged(propertyName);
        }

        protected void RegisterObservableCollection<T>(string propertyName, ObservableCollection<T> collection)
        {
            collection.CollectionChanged += (object? sender, NotifyCollectionChangedEventArgs e) =>
            {
                this.HandleObervableChildrenChanged(propertyName, e);
            };
        }
    }
}
