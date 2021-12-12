using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace PlexServiceTray
{
    public abstract class ObservableObject : INotifyPropertyChanged, IDataErrorInfo
    {
        protected object ValidationContext { get; set; }

        internal bool _isSelected;

        public virtual bool IsSelected
        {
            get => _isSelected;
            set {
                if (_isSelected == value) {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        private bool _isExpanded;

        public bool IsExpanded
        {
            set {
                if (_isExpanded == value) {
                    return;
                }

                _isExpanded = value;
                OnPropertyChanged("IsExpanded");
            }
        }

        #region PropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// This is required to create on property changed events
        /// </summary>
        /// <param name="name">What property of this object has changed</param>
        protected void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if(handler != null)
                handler(this, new PropertyChangedEventArgs(name));
            if (Validators.ContainsKey(name))
                UpdateError();
        }

        #endregion

        #region Data Validation

        private Dictionary<string, object> PropertyGetters
        {
            get
            {
                return GetType().GetProperties().Where(p => GetValidations(p).Length != 0).ToDictionary(p => p.Name, p => GetValueGetter(p));
            }
        }

        private Dictionary<string, ValidationAttribute[]> Validators
        {
            get
            {
                return GetType().GetProperties().Where(p => GetValidations(p).Length != 0).ToDictionary(p => p.Name, p => GetValidations(p));
            }
        }

        private ValidationAttribute[] GetValidations(PropertyInfo property)
        {
            return (ValidationAttribute[])property.GetCustomAttributes(typeof(ValidationAttribute), true);
        }

        private object GetValueGetter(PropertyInfo property)
        {
            return property.GetValue(this, null);
        }

        private string _error;

        public string Error => _error;

        private void UpdateError()
        {
            var errors = from i in Validators
                         from v in i.Value
                         where !Validate(v, PropertyGetters[i.Key])
                         select v.ErrorMessage;
            _error = string.Join(Environment.NewLine, errors.ToArray());
            OnPropertyChanged("Error");
        }

        public string this[string columnName]
        {
            get
            {
                if (PropertyGetters.ContainsKey(columnName))
                {
                    var value = PropertyGetters[columnName];
                    var errors = Validators[columnName].Where(v => !Validate(v, value))
                        .Select(v => v.ErrorMessage).ToArray();
                    OnPropertyChanged("Error");
                    return string.Join(Environment.NewLine, errors);
                }

                OnPropertyChanged("Error");
                return string.Empty;
            }
        }

        private bool Validate(ValidationAttribute v, object value)
        {
            return v.GetValidationResult(value, new ValidationContext(ValidationContext, null, null)) == ValidationResult.Success;
        }

        #endregion
    }
}
