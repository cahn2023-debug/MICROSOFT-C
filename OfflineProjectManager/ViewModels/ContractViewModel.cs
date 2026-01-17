using CommunityToolkit.Mvvm.ComponentModel;
using OfflineProjectManager.Models;
using System;

namespace OfflineProjectManager.ViewModels
{
    public class ContractViewModel : ObservableObject
    {
        private readonly Contract _model;

        public ContractViewModel(Contract model)
        {
            _model = model;
        }

        public Contract Model => _model;

        public int Id => _model.Id;

        public string ContractorName
        {
            get => _model.ContractorName;
            set
            {
                if (_model.ContractorName != value)
                {
                    _model.ContractorName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ContractCode
        {
            get => _model.ContractCode;
            set
            {
                if (_model.ContractCode != value)
                {
                    _model.ContractCode = value;
                    OnPropertyChanged();
                }
            }
        }

        public string BiddingPackage
        {
            get => _model.BiddingPackage;
            set
            {
                if (_model.BiddingPackage != value)
                {
                    _model.BiddingPackage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Content
        {
            get => _model.Content;
            set
            {
                if (_model.Content != value)
                {
                    _model.Content = value;
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

        public double Volume
        {
            get => _model.Volume;
            set
            {
                if (Math.Abs(_model.Volume - value) > 0.001)
                {
                    _model.Volume = value;
                    OnPropertyChanged();
                }
            }
        }

        public string VolumeUnit
        {
            get => _model.VolumeUnit;
            set
            {
                if (_model.VolumeUnit != value)
                {
                    _model.VolumeUnit = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Status
        {
            get => _model.Status;
            set
            {
                if (_model.Status != value)
                {
                    _model.Status = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime? StartDate
        {
            get => _model.StartDate;
            set
            {
                if (_model.StartDate != value)
                {
                    _model.StartDate = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime? EndDate
        {
            get => _model.EndDate;
            set
            {
                if (_model.EndDate != value)
                {
                    _model.EndDate = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
