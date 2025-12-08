
using WommeAPI.Models;
public interface ISyncService
{
    Task SyncDataAsync();
    Task<(List<JobMst> insertedRecords, List<JobMst> updatedRecords)> SyncJobMstAsync();
    Task<List<JobRouteMst>> SyncJobRouteAsync();
    Task<List<JobmatlMst>> SyncJobMatlMstAsync();
    Task<(List<WcMst> insertedRecords, List<WcMst> updatedRecords)> SyncWcMstAsync();
    Task<(List<EmployeeMst> insertedRecords, List<EmployeeMst> updatedRecords)> SyncEmployeeMstAsync();
    Task<List<JobTranMst>> SyncJobTranMstAsync();
    Task<List<JobSchMst>> SyncJobSchMstAsync();
    Task<List<ItemMst>> SyncItemMstAsync();
    Task<List<WomWcEmployee>> SyncWomWcEmployeeAsync();    

}
