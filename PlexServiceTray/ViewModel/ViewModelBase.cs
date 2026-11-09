using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlexServiceTray.ViewModel
{
    public abstract class ViewModelBase : INotifyPropertyChanged, IDataErrorInfo
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        /// <summary>
        /// Returns the validation error of a property or null
        /// </summary>
        protected virtual string Validate(string propertyName) => null;

        /// <summary>
        /// Names of the properties that are validated
        /// </summary>
        protected virtual IEnumerable<string> ValidatedProperties => new string[0];

        public bool HasErrors
        {
            get
            {
                foreach (var property in ValidatedProperties)
                {
                    if (!string.IsNullOrEmpty(Validate(property))) return true;
                }
                return false;
            }
        }

        public string Error
        {
            get
            {
                foreach (var property in ValidatedProperties)
                {
                    var error = Validate(property);
                    if (!string.IsNullOrEmpty(error)) return error;
                }
                return null;
            }
        }

        public string this[string columnName] => Validate(columnName);
    }
}
