﻿using Microsoft.Extensions.Options;

using Our.Umbraco.MaintenanceMode.Configurations;
using Our.Umbraco.MaintenanceMode.Factories;
using Our.Umbraco.MaintenanceMode.Interfaces;
using Our.Umbraco.MaintenanceMode.Models;
using Our.Umbraco.MaintenanceMode.Providers;

using Serilog;

using System.Threading.Tasks;

namespace Our.Umbraco.MaintenanceMode.Services
{
    public class MaintenanceModeService : IMaintenanceModeService
    {
        private readonly ILogger _logger;
        private readonly IStorageProviderFactory _storageProviderFactory; 
        
        private readonly Configurations.MaintenanceModeSettings _maintenanceModeSettings;
        private readonly string _configFilePath;
        private MaintenanceModeStatus TrackedStatus { get; set; }

        public MaintenanceModeStatus Status
        {
            get
            {
                // when in 'Database' storage mode we want to be fetching every time as this
                // typically will mean Umbraco is deployed in a distributed environment therefore
                // status can't be tracked in scope, it needs to be read from storage each time
                return _storageProviderFactory.StorageMode switch
                {
                    StorageMode.Database => StorageProvider.Read().Result,
                    _ => TrackedStatus
                };
            }
        }

        public MaintenanceModeService(ILogger logger,
            IOptions<Configurations.MaintenanceModeSettings> maintenanceModeSettings, 
            IStorageProviderFactory storageProviderFactory)
        {
            _logger = logger;
            _storageProviderFactory = storageProviderFactory;
            _maintenanceModeSettings = maintenanceModeSettings.Value;

            TrackedStatus = LoadStatus().Result;
        }

        public IStorageProvider StorageProvider => _storageProviderFactory.GetProvider();

        public async Task ToggleMaintenanceMode(bool maintenanceMode)
        {
            // checking against TrackedStatus is fine even in distributed environments
            // the toggle will have been executed on the SchedulingPublisher app
            if (maintenanceMode == TrackedStatus.IsInMaintenanceMode)
                return; // already in this state

            TrackedStatus.IsInMaintenanceMode = maintenanceMode;
            await StorageProvider.Save(TrackedStatus);
        }

        public async Task ToggleContentFreeze(bool isContentFrozen)
        {
            // checking against TrackedStatus is fine even in distributed environments
            // the toggle will have been executed on the SchedulingPublisher app
            if (isContentFrozen == TrackedStatus.IsContentFrozen)
                return; // already in this state

            TrackedStatus.IsContentFrozen = isContentFrozen;
            await StorageProvider.Save(TrackedStatus);
        }

        public async Task SaveSettings(Models.MaintenanceModeSettings settings)
        {
            TrackedStatus.Settings = settings;
            await StorageProvider.Save(TrackedStatus);
        }

        private async Task<MaintenanceModeStatus> LoadStatus()
        {
            // fallback - defaults set in the service code
            var maintenanceModeStatus = new MaintenanceModeStatus
            {
                IsInMaintenanceMode = false,
                UsingWebConfig = false,
                Settings = new Models.MaintenanceModeSettings
                {
                    ViewModel = new Models.MaintenanceMode()
                }
            };

            // read from the storage location, if available
            maintenanceModeStatus = await CheckStorage(maintenanceModeStatus);

            // override from appsettings, if applicable
            return CheckAppSettings(maintenanceModeStatus);
        }

        private MaintenanceModeStatus CheckAppSettings(MaintenanceModeStatus status)
        {
            if (_maintenanceModeSettings is null or { IsInMaintenanceMode: false })
                return status;

            status.IsInMaintenanceMode = _maintenanceModeSettings.IsInMaintenanceMode;
            status.IsContentFrozen = _maintenanceModeSettings.IsContentFrozen;
            status.UsingWebConfig = true;

            return status;
        }

        private async Task<MaintenanceModeStatus> CheckStorage(MaintenanceModeStatus status) 
            => await StorageProvider.Read() ?? status;
    }
}
