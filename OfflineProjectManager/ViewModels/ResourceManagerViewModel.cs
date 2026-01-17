using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using OfflineProjectManager.Models;
using OfflineProjectManager.Services;
using System.Linq;

namespace OfflineProjectManager.ViewModels
{
    public class ResourceManagerViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;

        public ObservableCollection<PersonnelViewModel> PersonnelHelper { get; set; } = [];
        public ObservableCollection<ContractViewModel> ContractHelper { get; set; } = [];

        private PersonnelViewModel _selectedPersonnel;
        public PersonnelViewModel SelectedPersonnel
        {
            get => _selectedPersonnel;
            set
            {
                if (SetProperty(ref _selectedPersonnel, value))
                {
                    // Do something if needed
                }
            }
        }

        private ContractViewModel _selectedContract;
        public ContractViewModel SelectedContract
        {
            get => _selectedContract;
            set
            {
                if (SetProperty(ref _selectedContract, value))
                {
                    // Do something if needed
                }
            }
        }

        // New Item Inputs (Personnel)
        public string NewPersonName { get; set; }
        public string NewPersonPhone { get; set; }
        public string NewPersonRegion { get; set; }
        public string NewPersonRole { get; set; }

        // New Item Inputs (Contract)
        public string NewContractorName { get; set; }
        public string NewContractCode { get; set; }
        public string NewContractContent { get; set; }
        public string NewContractPackage { get; set; }
        public string NewContractRegion { get; set; }
        public double NewContractVolume { get; set; }
        public string NewContractUnit { get; set; }

        public ICommand RefreshCommand { get; }
        public ICommand AddPersonnelCommand { get; }
        public ICommand AddContractCommand { get; }
        public ICommand SavePersonnelCommand { get; }
        public ICommand SaveContractCommand { get; }
        public ICommand DeletePersonnelCommand { get; }
        public ICommand DeleteContractCommand { get; }

        public ResourceManagerViewModel(IProjectService projectService)
        {
            _projectService = projectService;

            RefreshCommand = new AsyncRelayCommand(LoadData);
            AddPersonnelCommand = new AsyncRelayCommand(AddPersonnel);
            AddContractCommand = new AsyncRelayCommand(AddContract);
            SavePersonnelCommand = new AsyncRelayCommand(SavePersonnel);
            SaveContractCommand = new AsyncRelayCommand(SaveContract);
            DeletePersonnelCommand = new AsyncRelayCommand(DeletePersonnel);
            DeleteContractCommand = new AsyncRelayCommand(DeleteContract);
        }

        public async Task LoadData()
        {
            PersonnelHelper.Clear();
            ContractHelper.Clear();

            if (!_projectService.IsProjectOpen) return;

            var people = await _projectService.GetPersonnelAsync();
            foreach (var p in people) PersonnelHelper.Add(new PersonnelViewModel(p));

            var contracts = await _projectService.GetContractsAsync();
            foreach (var c in contracts) ContractHelper.Add(new ContractViewModel(c));
        }

        private async Task AddPersonnel()
        {
            if (string.IsNullOrWhiteSpace(NewPersonName)) return;

            var p = new Personnel
            {
                Name = NewPersonName,
                Phone = NewPersonPhone,
                Region = NewPersonRegion,
                Role = NewPersonRole
            };

            p = await _projectService.AddPersonnelAsync(p);
            PersonnelHelper.Add(new PersonnelViewModel(p));

            // Clear inputs (Binding requires PropertyChanged, simplified here)
            NewPersonName = "";
            OnPropertyChanged(nameof(NewPersonName));
        }

        private async Task AddContract()
        {
            if (string.IsNullOrWhiteSpace(NewContractorName)) return;

            var c = new Contract
            {
                ContractorName = NewContractorName,
                ContractCode = NewContractCode,
                BiddingPackage = NewContractPackage,
                Content = NewContractContent,
                Region = NewContractRegion,
                Volume = NewContractVolume,
                VolumeUnit = NewContractUnit,
                Status = "Active"
            };

            c = await _projectService.AddContractAsync(c);
            ContractHelper.Add(new ContractViewModel(c));

            NewContractorName = "";
            OnPropertyChanged(nameof(NewContractorName));
        }

        private async Task SavePersonnel()
        {
            if (SelectedPersonnel != null)
            {
                await _projectService.UpdatePersonnelAsync(SelectedPersonnel.Model);
                System.Windows.MessageBox.Show("Saved Personnel!");
            }
        }

        private async Task SaveContract()
        {
            if (SelectedContract != null)
            {
                await _projectService.UpdateContractAsync(SelectedContract.Model);
                System.Windows.MessageBox.Show("Saved Contract!");
            }
        }

        private async Task DeletePersonnel()
        {
            if (SelectedPersonnel != null)
            {
                if (System.Windows.MessageBox.Show("Delete this personnel?", "Confirm", System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
                {
                    await _projectService.DeletePersonnelAsync(SelectedPersonnel.Id);
                    PersonnelHelper.Remove(SelectedPersonnel);
                }
            }
        }

        private async Task DeleteContract()
        {
            if (SelectedContract != null)
            {
                if (System.Windows.MessageBox.Show("Delete this contract?", "Confirm", System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
                {
                    await _projectService.DeleteContractAsync(SelectedContract.Id);
                    ContractHelper.Remove(SelectedContract);
                }
            }
        }
    }
}
