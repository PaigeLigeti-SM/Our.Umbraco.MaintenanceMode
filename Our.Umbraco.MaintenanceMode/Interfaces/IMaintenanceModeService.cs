﻿using System.Threading.Tasks;
using Our.Umbraco.MaintenanceMode.Models;

namespace Our.Umbraco.MaintenanceMode.Interfaces
{
    public interface IMaintenanceModeService
    {
        Task ToggleMaintenanceMode(bool maintenanceMode);
        Task ToggleContentFreeze(bool isContentFrozen);
        MaintenanceModeStatus Status { get; }
        Task SaveSettings(MaintenanceModeSettings settings);
    }
}
