using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;

namespace Orleans.Activities.Designers.Binding
{
    // To avoid NotSupportedException("This type of CollectionView does not support changes to its SourceCollection from a thread different from the Dispatcher thread")
    // when modifying ComboBox's ItemsSource from Constraints we must use the UI's Dispacher, sadly this supported only in OnPropertyChanged and not in OnCollectionChanged.
    // We use Constraints to analyse the Activities and to store the collected information in non-serialized "design time" properties.

    // TODO can we use Attached Properties instead of non-serialized "design time" properties???

    // http://geekswithblogs.net/NewThingsILearned/archive/2008/01/16/have-worker-thread-update-observablecollection-that-is-bound-to-a.aspx
    // But Dispatcher.Invoke() replaced with Dispatcher.BeginInvoke() to avoid designer freeze.
    // And added the Set() method.

    /// <summary>
    /// Makes collection properties modifiable during design time by validation constraints running in the background.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObservableCollection<T> : System.Collections.ObjectModel.ObservableCollection<T>
    {
        // Override the event so this class can access it
        public override event System.Collections.Specialized.NotifyCollectionChangedEventHandler CollectionChanged;

        protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Be nice - use BlockReentrancy like MSDN said
            using (BlockReentrancy())
            {
                var eventHandler = CollectionChanged;
                if (eventHandler == null)
                    return;

                var delegates = eventHandler.GetInvocationList();
                // Walk thru invocation list
                foreach (System.Collections.Specialized.NotifyCollectionChangedEventHandler handler in delegates)
                {
                    // If the subscriber is a DispatcherObject and different thread
                    if (handler.Target is DispatcherObject dispatcherObject && dispatcherObject.CheckAccess() == false)
                    {
                        // Invoke handler in the target dispatcher's thread
                        dispatcherObject.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, handler, this, e);
                    }
                    else // Execute handler as is
                        handler(this, e);
                }
            }
        }

        public void Set(IEnumerable<T> values)
        {
            var valueArray = values?.ToArray();
            var valueArrayLength = valueArray?.Length ?? 0;
            var equal = (this.Count == valueArrayLength);
            if (equal)
                for (var i = 0; i < valueArrayLength; i++)
                    if (!object.Equals(valueArray[i], this[i]))
                    {
                        equal = false;
                        break;
                    }
            if (!equal)
            {
                Clear();
                if (valueArray != null)
                    foreach (var value in valueArray)
                        Add(value);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(this.Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}
