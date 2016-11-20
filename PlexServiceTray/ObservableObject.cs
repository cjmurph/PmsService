using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PlexServiceTray
{
    public abstract class ObservableObject : INotifyPropertyChanged, IDataErrorInfo
    {
        protected object ValidationContext { get; set; }

        protected bool _isSelected;

        public virtual bool IsSelected
        {
            get
            {
                return _isSelected;
            }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        protected bool _isExpanded;

        public virtual bool IsExpanded
        {
            get
            {
                return _isExpanded;
            }
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged("IsExpanded");
                }
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
            if (validators.ContainsKey(name))
                UpdateError();
        }

        #endregion

        #region Data Validation

        private Dictionary<string, object> propertyGetters
        {
            get
            {
                return this.GetType().GetProperties().Where(p => getValidations(p).Length != 0).ToDictionary(p => p.Name, p => getValueGetter(p));
            }
        }

        private Dictionary<string, ValidationAttribute[]> validators
        {
            get
            {
                return this.GetType().GetProperties().Where(p => getValidations(p).Length != 0).ToDictionary(p => p.Name, p => getValidations(p));
            }
        }

        private ValidationAttribute[] getValidations(PropertyInfo property)
        {
            return (ValidationAttribute[])property.GetCustomAttributes(typeof(ValidationAttribute), true);
        }

        private object getValueGetter(PropertyInfo property)
        {
            return property.GetValue(this, null);
        }

        private string _error;

        public string Error
        {
            get
            {
                return _error;
            }
        }

        private void UpdateError()
        {
            var errors = from i in validators
                         from v in i.Value
                         where !validate(v, propertyGetters[i.Key])
                         select v.ErrorMessage;
            _error = string.Join(Environment.NewLine, errors.ToArray());
            OnPropertyChanged("Error");
        }

        public string this[string columnName]
        {
            get
            {
                if (propertyGetters.ContainsKey(columnName))
                {
                    var value = propertyGetters[columnName];
                    var errors = validators[columnName].Where(v => !validate(v, value))
                        .Select(v => v.ErrorMessage).ToArray();
                    OnPropertyChanged("Error");
                    return string.Join(Environment.NewLine, errors);
                }

                OnPropertyChanged("Error");
                return string.Empty;
            }
        }

        private bool validate(ValidationAttribute v, object value)
        {
            return v.GetValidationResult(value, new ValidationContext(ValidationContext, null, null)) == ValidationResult.Success;
        }

        #endregion
    }
}
