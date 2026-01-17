using CommunityToolkit.Mvvm.ComponentModel;
using OfflineProjectManager.Models;

namespace OfflineProjectManager.ViewModels
{
    public class PersonnelViewModel : ObservableObject
    {
        private readonly Personnel _model;

        public PersonnelViewModel(Personnel model)
        {
            _model = model;
        }

        public Personnel Model => _model;

        public int Id => _model.Id;

        public string Name
        {
            get => _model.Name;
            set
            {
                if (_model.Name != value)
                {
                    _model.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Phone
        {
            get => _model.Phone;
            set
            {
                if (_model.Phone != value)
                {
                    _model.Phone = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Region
        {
            get => _model.Region;
            set
            {
                if (_model.Region != value)
                {
                    _model.Region = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Role
        {
            get => _model.Role;
            set
            {
                if (_model.Role != value)
                {
                    _model.Role = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
