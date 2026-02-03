import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthServices } from './auth.service'; 
import { environment } from '../../environments/environment';

export interface AssignedJob {
  employeeCode: string;
  username: string;
  jobNumber: string;
  jobName: string;
  assignedHours: number;
  remark: string;
  createdAt: string;
  updatedAt: string;
  entryNo: number;

}

@Injectable({
  providedIn: 'root'
})
export class JobService {
  private baseUrl = environment.apiBaseUrl;
  public fileBaseUrl = environment.fileBaseUrl;

  constructor(
    private http: HttpClient,
    private authService: AuthServices // Injected
  ) {}


  getLocalDateTime(): string {
                    const now = new Date();
                    return now.toLocaleString('sv-SE', {
                      year: 'numeric',
                      month: '2-digit',
                      day: '2-digit',
                      hour: '2-digit',
                      minute: '2-digit',
                      second: '2-digit',
                      hour12: false
                    }).replace(' ', 'T') + '.' + String(now.getMilliseconds()).padStart(3, '0')
                      .replace('T', ' ');
  }




  // Centralized reusable header method
  private getHeaders(): HttpHeaders {
    const jwt = this.authService.getJwtToken(); // ✅ Get from memory
    return new HttpHeaders({
      Authorization: `Bearer ${jwt}`
    });
  }

  //All GET API


  GetPostedTransactions(page: number = 0, size: number = 50, search: string = ''): Observable<{ data: AssignedJob[], total: number }> {
    return this.http.get<{ data: AssignedJob[], total: number }>(
      `${this.baseUrl}/Get/GetPostedTransactions?page=${page}&size=${size}&search=${search}`,
      { headers: this.getHeaders() }
    );
  }

 GetJobUnpostedTransFullDetails(jobId: string, operNum: string, trans_num:string): Observable<any> {
  // Make sure the query param names match backend: job & operNum
  return this.http.get<any>(`${this.baseUrl}/Get/GetJobUnpostedTransFullDetails`, {
    headers: this.getHeaders(),
    params: {
      job: jobId,
      operNum: operNum,
      trans_num:trans_num,
      
    }
  });
 }

 GetJobPostedTransFullDetails(jobId: string, operNum: string, trans_num:string): Observable<any> {
  // Make sure the query param names match backend: job & operNum
  return this.http.get<any>(`${this.baseUrl}/Get/GetJobPostedTransFullDetails`, {
    headers: this.getHeaders(),
    params: {
      job: jobId,
      operNum: operNum,
      trans_num:trans_num,
      
    }
  });
 }


//  GetJobPoolData(
//     page: number = 0,
//     size: number = 50,
//     search: string = '',
//     emp_num: string | null = null
//   ): Observable<{ data: any[], total: number }> {
    
//     let url = `${this.baseUrl}/Get/GetJobPoolData?page=${page}&size=${size}&search=${search}`;
    
//     if (emp_num) {
//       url += `&emp_num=${emp_num}`; 
//     }

//     return this.http.get<{ data: any[], total: number }>(url, {
//       headers: this.getHeaders()
//     });
//   }

  GetJobPoolData(page: number = 0, size: number = 50, search: string = ''): Observable<{ data: any[], total: number }> {
    return this.http.get<{ data: any[], total: number }>(
      `${this.baseUrl}/Get/GetJobPoolData?page=${page}&size=${size}&search=${search}`,
      { headers: this.getHeaders() }
    );
  }


  GetJobPoolAllData(page: number, size: number, search: string) {
    return this.http.get<{ data: any[], total: number }>(
      `${this.baseUrl}/Get/GetJobPoolAllData?page=${page}&size=${size}&search=${search}`,
      { headers: this.getHeaders() }
    );
  }



  GetJobs(page: number = 0, size: number = 50, search: string = ''): Observable<{ data: AssignedJob[], total: number }> {
    return this.http.get<{ data: AssignedJob[], total: number }>(
      `${this.baseUrl}/Get/GetJobs?page=${page}&size=${size}&search=${search}`,
      { headers: this.getHeaders() }
    );
  }

  GetUnpostedTransactions(page: number, size: number, search: string, employeeCode:string) {
    return this.http.get<{ data: any[], total: number }>(
      `${this.baseUrl}/Get/GetUnpostedTransactions?page=${page}&size=${size}&search=${search}&emp_num=${employeeCode}`,
      { headers: this.getHeaders() }
    );
  }


  GetIssuedTransactions(page: number, size: number, search: string, employeeCode:string) {
    return this.http.get<{ data: any[], total: number }>(
      `${this.baseUrl}/Get/GetIssuedTransactions?page=${page}&size=${size}&search=${search}&emp_num=${employeeCode}`,
      { headers: this.getHeaders() }
    );
  }

   getVerifyTransactions(page: number, size: number, search: string, employeeCode:string) {
      return this.http.get<{ data: any[], total: number }>(
        `${this.baseUrl}/Get/GetVerifyTransactions?page=${page}&size=${size}&search=${search}&emp_num=${employeeCode}`,
        { headers: this.getHeaders() }
      );
    }
   

    GetCompletedVerifyJob(): Observable<{ data: any[], totalRecords: number }> {
      return this.http.get<{ data: any[], totalRecords: number }>(
        `${this.baseUrl}/Get/GetCompletedVerifyJob`,
        { headers: this.getHeaders() }
      );
    }


   


  canStartJob(jobNumber: string, serialNo: string, operationNumber: number) {
    return this.http.get<{ success: boolean; message: string; canStart: boolean }>(
      `${this.baseUrl}/Get/canStartJob`,
      {
        headers: this.getHeaders(),
        params: {
          job: jobNumber,
          serialNo: serialNo,
          operNum: operationNumber
        }
      }
    );
  }

  getIsNextJobActive(): Observable<AssignedJob[]> {
    return this.http.get<AssignedJob[]>(`${this.baseUrl}/Get/IsNextJobActive`, {
      headers: this.getHeaders()
    });
  }



  GetQC(page: number, size: number, search: string) {
    return this.http.get<{ data: any[], total: number }>(
      `${this.baseUrl}/Get/GetQC?page=${page}&size=${size}&search=${search}`,
      { headers: this.getHeaders() }
    );
  }

  GetOnlyQCJobs(page: number, size: number, search: string) {
    return this.http.get<{ data: any[], total: number }>(
      `${this.baseUrl}/Get/GetOnlyQCJobs?page=${page}&size=${size}&search=${search}`,
      { headers: this.getHeaders() }
    );
  }


  getEmployeesForJob(jobNumber: string, operationNumber: string) {
    return this.http.get<any[]>(
      `${this.baseUrl}/Get/GetEmployeesForJob/${jobNumber}/${operationNumber}`,
      { headers: this.getHeaders() }
    );
  }

  getMachinesForJob(jobNumber: string, operationNumber: string) {
    return this.http.get<any[]>(
      `${this.baseUrl}/Get/GetMachinesForJob/${jobNumber}/${operationNumber}`,
      { headers: this.getHeaders() }
    );
  }
  
  

  


  

  
  //  API: Get Assigned Jobs
  getAssignedJobs(): Observable<AssignedJob[]> {
    return this.http.get<AssignedJob[]>(`${this.baseUrl}/Get/AssignedJobs`, {
      headers: this.getHeaders()
    });
  }




 //  NEW: Get all roles
  getAllRole(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/Get/GetRoleMasters`, {
      headers: this.getHeaders()
    });
  }

  //  NEW: Get all pages
  getAllPages(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/Get/GetPageMasters`, {
      headers: this.getHeaders()
    });
  }
  
  //  API: Get Page Permissions for a Role
  getPagePermissionsByRole(roleID: number): Observable<any[]> {
    return this.http.get<any[]>(
      `${this.baseUrl}/Get/RolePageMappings?roleID=${roleID}`,
      { headers: this.getHeaders() }
    );
  }

  UserMaster(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/Get/UserMaster`, {
      headers: this.getHeaders()  
    });
  }

  getMachineMasters(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/Get/GetMachineMasters`, {
      headers: this.getHeaders()
    });
  }

  GetItems(page: number = 0, size: number = 50, search: string = ''): Observable<{ data: any[], total: number }> {
    return this.http.get<{ data: any[], total: number }>(
      `${this.baseUrl}/Get/GetItems?page=${page}&size=${size}&search=${search}`,
      { headers: this.getHeaders() }
    );
  }

  GetEmployees(page: number = 0, size: number = 50, search: string = ''): Observable<{ data: any[], total: number }> {
    return this.http.get<{ data: any[], total: number }>(
      `${this.baseUrl}/Get/GetEmployees?page=${page}&size=${size}&search=${search}`,
      { headers: this.getHeaders() }
    );
  }

 GetDistinctOperations(page: number = 0, size: number = 50, search: string = ''): Observable<{ data: number[], total: number }> {
  return this.http.get<{ data: number[], total: number }>(
    `${this.baseUrl}/Get/GetDistinctOperations?page=${page}&size=${size}&search=${search}`
  );
}


  GetJobReport(jobId: string) {
    return this.http.get<any>(`${this.baseUrl}/Get/GetJobReport/${jobId}`);
  }


   GetWorkCenters(page: number = 0, size: number = 50, search: string = ''): Observable<{ data: number[], total: number }> {
    return this.http.get<{ data: number[], total: number }>(
      `${this.baseUrl}/Get/GetWorkCenters?page=${page}&size=${size}&search=${search}`
    );
  }

  

  getTotalUsers(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/Get/GetTotalUsers`, {
      headers: this.getHeaders()
    });
  }

  getunpostedJobs(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/Get/getunpostedJobs`, {
      headers: this.getHeaders()
    });
  }

  PostedJobsCount(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/Get/PostedJobsCount`, {
      headers: this.getHeaders()
    });
  }

  ActiveJobsCount(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/Get/ActiveJobsCount`, {
      headers: this.getHeaders()
    });
  }

  PausedJobsCount(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/Get/PausedJobsCount`, {
      headers: this.getHeaders()
    });
  }

  excdJobsCount(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/Get/excdJobsCount`, {
      headers: this.getHeaders()
    });
  }

  getNewJobs(): Observable<AssignedJob[]> {
    return this.http.get<AssignedJob[]>(`${this.baseUrl}/Get/GetNewJobs`, {
      headers: this.getHeaders()
    });
  }

  getActiveOvertimeJobCount(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/Get/GetActiveOvertimeJobCount`, {
      headers: this.getHeaders()
    });
  }
  
  GetAllCalendars() {
    return this.http.get<any[]>(`${this.baseUrl}/Calendar/GetAllCalendars`,{
     headers: this.getHeaders()
    }); 
  }

  
  GetAllWcMachines() {
    return this.http.get<any[]>(`${this.baseUrl}/Get/GetAllWcMachines`,{
            headers: this.getHeaders()
    });
  }
  getAllEmployees(){
    return this.http.get<any[]>(`${this.baseUrl}/Get/getAllEmployees`,{
            headers: this.getHeaders()
    });
}
  getAllMachines(){
    return this.http.get<any[]>(`${this.baseUrl}/Get/getAllMachines`,{
            headers: this.getHeaders()
    });
  }
//all delete
  deleteAssignedJob(entryNo: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/delete/DeleteAssignedJob/${entryNo}`, {
      headers: this.getHeaders()
    });
  }

deleteJobTransaction(jobNumber: string, serialNumber: string, operationNumber: number): Observable<any> {
  const body = { 
    jobNumber, 
    serialNo: serialNumber,
    oper_num: operationNumber 
  };

  return this.http.delete<any>(
    `${this.baseUrl}/delete/DeleteJobTransaction`,
    { headers: this.getHeaders(), body }
  );
}


  deleteMachineMaster(entryNo: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/delete/DeleteMachineMaster/${entryNo}`, {
      headers: this.getHeaders()
    });
  }

  deleteRoleMaster(entryNo: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/delete/DeleteRoleMaster/${entryNo}`, {
      headers: this.getHeaders()
    });
  }
  deleteCalendar(id:number):Observable<any>{
    return this,this.http.delete<any>(`${this.baseUrl}/Calendar/DeleteCalendar/${id}`,{
          headers: this.getHeaders()
    })
  }
  deleteRolePageMapping(entryNo: number) {
    return this.http.delete<any>(
      `${this.baseUrl}/delete/DeleteRolePageMapping/${entryNo}`,
      { headers: this.getHeaders() }
    );
  } 
  deleteMachineEmployee(machineNum: string, empNum: string) {
  return this.http.delete(
    `${this.baseUrl}/delete/DeleteMachineEmployee/${machineNum}/${empNum}`,{
       headers: this.getHeaders()
    }
  );
}

deleteMachinewc(machineNumber: string, wcCode: string) {
  return this.http.delete(
    `${this.baseUrl}/delete/DeleteMachineWC/${machineNumber}/${wcCode}`,{
       headers: this.getHeaders()
    }
  );
}

//all post

  createMachine(machine: {
    machineNumber: number | null;
    machineName: string;
    machineDescription: string;
  }): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Post/CreateMachine`, machine, {
      headers: this.getHeaders()
    });
}

CheckPrevJob(jobData: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Post/CheckPrevJob`, jobData, {
      headers: this.getHeaders()
    });
  }




  updateJobLog(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Post/UpdateJobLog`, payload, {
      headers: this.getHeaders()
    });
  }

  submitTransaction(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Post/submit-transaction`, payload, {
      headers: this.getHeaders()
    });
  }

  verifyTransaction(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Post/verify-transaction`, payload, {
      headers: this.getHeaders()
    });
  }




  JobPoolDetails(poolNumber: string): Observable<any> {
    return this.http.post<any>(
      `${this.baseUrl}/Post/JobPoolDetails`,
      { jobPoolNumber: poolNumber },   
      { headers: this.getHeaders() }
    );
  }

  jobPoolHold(poolNumber: string): Observable<any> {
    return this.http.post<any>(
      `${this.baseUrl}/Post/jobPoolHold`,
      { jobPoolNumber: poolNumber },   
      { headers: this.getHeaders() }
    );
  }

  jobPoolComplete(poolNumber: string): Observable<any> {
    return this.http.post<any>(
      `${this.baseUrl}/Post/jobPoolComplete`,
      { jobPoolNumber: poolNumber },   
      { headers: this.getHeaders() }
    );
  }

  jobPoolresume(poolNumber: string): Observable<any> {
    return this.http.post<any>(
      `${this.baseUrl}/Post/jobPoolresume`,
      { jobPoolNumber: poolNumber },   
      { headers: this.getHeaders() }
    );
  }


  jobpoolstart(jobData: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Post/jobpoolstart`, jobData, {
      headers: this.getHeaders()
    });
  }

  getJobPoolStatus(jobPoolNumber: string): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Get/jobpoolstatus/${jobPoolNumber}`, {
      headers: this.getHeaders()
    });
  }

  GetActiveJobTransactions(): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Get/GetActiveJobTransactions`, {
      headers: this.getHeaders()
    });
  }

  GetActiveQCJobs(): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Get/GetActiveQCJobs`, {
      headers: this.getHeaders()
    });
  }

  GetWorkCenterMaster(): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Get/GetWorkCenterMaster`, {
      headers: this.getHeaders()
    });
  }



  createAssignedJob(data: {
    employeeCode: string;
    jobNumber: string;
    assignedHours: number;
    remark: string;
  }) {
    return this.http.post<any>(`${this.baseUrl}/Post/CreateAssignedJob`, data,{
    headers: this.getHeaders() 
    
    });
  }
  getJobTrans(job: string) {
    return this.http.get<any[]>(
      `${this.baseUrl}/Post/JobTransbyjob?job=${job}`,
      { headers: this.getHeaders() }
    );
  }
  addCalendar(payload: { date: string; flag: number }) {
    return this.http.post(`${this.baseUrl}/Calendar/AddCalendar`, payload,{
    headers: this.getHeaders()
    });
  }
// createEmployee(data: {
//   employeeCode: string;
//   userName: string;
//   passwordHash: string;
//   roleID: number;       
//   isActive: boolean;
// }): Observable<any> {
//   return this.http.post<any>(`${this.baseUrl}/Post/AddUser`, data, {
//     headers: this.getHeaders()
//   });
// }


  addEmployeeLog(payload: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/Post/AddEmployeeLog`, payload,{
    headers: this.getHeaders()

    });
  }

   getJobProgress(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/Get/getJobProgress`,{
              headers: this.getHeaders()
      });    
    }

  startJob(payload: any): Observable<any> {
    const url = `${this.baseUrl}/Post/StartJob`;
    return this.http.post(url, payload, { headers: this.getHeaders() });
  }  

  PauseJob(payload: any): Observable<any> {
    const url = `${this.baseUrl}/Post/PauseJob`;
    return this.http.post(url, payload, { headers: this.getHeaders() });
  }


  CompleteJob(payload: any): Observable<any> {
    const url = `${this.baseUrl}/Post/CompleteJob`;
    return this.http.post(url, payload, { headers: this.getHeaders() });
  }

  startIssueJob(payload: any): Observable<any> {
    const url = `${this.baseUrl}/Post/startIssueJob`;
    return this.http.post(url, payload, { headers: this.getHeaders() });
  }

  startQCJob(payload: any): Observable<any> {
    const url = `${this.baseUrl}/Post/StartSingleQCJob`;
    return this.http.post(url, payload, { headers: this.getHeaders() });
  }

  startGroupQCJobs(payload: any): Observable<any> {
    const url = `${this.baseUrl}/Post/StartGroupQCJobs`;
    return this.http.post(url, payload, { headers: this.getHeaders() });
  }

    /** 🔹 Pause multiple jobs (Group QC) */
  pauseGroupQCJobs(payload: any): Observable<any> {
    const url = `${this.baseUrl}/Post/PauseGroupQCJobs`;
    return this.http.post(url, payload, { headers: this.getHeaders() });
  }

  /** 🔹 Complete multiple jobs (Group QC) */
  completeGroupQCJobs(payload: any): Observable<any> {
    const url = `${this.baseUrl}/Post/CompleteGroupQCJobs`;
    return this.http.post(url, payload, { headers: this.getHeaders() });
  }

  EmpMechCodeChecker(job: string, operation: string, transNum: string, serialNo: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Post/EmpMechCodeChecker`, {
    job,
    operation,
    transNum,
    serialNo
    });
  }



   getNotifications(): Observable<AssignedJob[]> {
    return this.http.get<AssignedJob[]>(`${this.baseUrl}/Get/getNotifications`, {
      headers: this.getHeaders()
    });
  }
  

respondToNotification(payload: any): Observable<any> {
  const url = `${this.baseUrl}/Post/notifications`;
  return this.http.post(url, payload, { headers: this.getHeaders() });
}



  


  CheckJobStatus(job: string, operation: string, transNum: string, serialNo: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Post/CheckJobStatus`, {
    job,
    operation,
    transNum,
    serialNo
    });
  }

  importCalendar(payload: any[]) {
    return this.http.post(
      `${this.baseUrl}/Post/importCalendar`,
      payload,                         
      { headers: this.getHeaders() }   
    );
  }



  unpostedCheckJobStatus(): Observable<AssignedJob[]> {
    return this.http.get<AssignedJob[]>(`${this.baseUrl}/Get/unpostedCheckJobStatus`, {
      headers: this.getHeaders()
    });
  }
  

  pauseJob(payload: any): Observable<any> {
    const url = `${this.baseUrl}/Post/PauseJob`;
    return this.http.post(url, payload, { headers: this.getHeaders() });
  }

  completeJob(payload: any): Observable<any> {
    const url = `${this.baseUrl}/Post/CompleteJob`;
    return this.http.post(url, payload, { headers: this.getHeaders() });
  }


   CreateRolePageMapping(mapping: { roleID: number; pageID: number }) {
    return this.http.post<any>(
      `${this.baseUrl}/Post/CreateRolePageMapping`,
      mapping,
      { headers: this.getHeaders() }
    );
  }
    addEmployee(employee: any): Observable<any> {
      return this.http.post(`${this.baseUrl}/Post/AddEmployee`, employee,
      { headers: this.getHeaders() }

      );
    }
    AddMachineWc(payload: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/post/AddMachineWc`, payload);
  }
 SyncJobMst() {
    return this.http.post(`${this.baseUrl}/scheduler/SyncJobMst`, {}, {
      headers: this.getHeaders(),
      responseType: 'text' as 'json'   
    });
}

SyncAllTables() {
  return this.http.post(`${this.baseUrl}/scheduler/SyncAllTablesAfterSept2025`, {}, {
    headers: this.getHeaders(),
    responseType: 'text' as 'json'
  });
}

  SyncItemMst() {
    return this.http.post(`${this.baseUrl}/scheduler/SyncItemMst`, {}, {
      headers: this.getHeaders(),
      responseType: 'text' as 'json'   
    });
}

  SyncJobTranMst() {
    return this.http.post(`${this.baseUrl}/scheduler/SyncJobTranMst`, {}, {
      headers: this.getHeaders(),
      responseType: 'text' as 'json'   
    });
  }

  SyncEmployeeMst() {
    return this.http.post(`${this.baseUrl}/scheduler/SyncEmployeeMst`, {}, {
      headers: this.getHeaders(),
      responseType: 'text' as 'json'   
    });
  }
    SyncWcMst() {
    return this.http.post(`${this.baseUrl}/scheduler/SyncWcMst`, {}, {
      headers: this.getHeaders(),
      responseType: 'text' as 'json'   
    });
  }

//all put
  updateAssignedJob(entryNo: number, payload: any) {
    return this.http.put<any>(`${this.baseUrl}/update/UpdateAssignedJob/${entryNo}`, payload, {
      headers: this.getHeaders()
    });
  }

  updateMachineMaster(entryNo: number, data: any): Observable<any> {
    return this.http.put(`${this.baseUrl}/update/UpdateMachineMaster/${entryNo}`, data, {
      headers: this.getHeaders()
    });
  }


//   updateEmployee(entryNo: number, data: any): Observable<any> { 
//     return this.http.put(`${this.baseUrl}/update/UpdateUser/${entryNo}`, data, {
//       headers: this.getHeaders()
//     });
//  }
 
  updateUserProfileImages(formData: FormData) {
      return this.http.put(`${this.baseUrl}/update/UpdateUserProfileImages`, formData,
        {
         headers: this.getHeaders()

        });
  }


updateEmployee(empNum: string, employee: any): Observable<any> {
  return this.http.put(`${this.baseUrl}/update/UpdateEmployee/${empNum}`, employee,{
       headers: this.getHeaders()
    });
}

updateMachineEmployee(machineNumber: string, payload: any) {
  return this.http.put(
    `${this.baseUrl}/update/EditMachineEmployee/${machineNumber}`,
    payload
  );
}

 //All Download QR
  downloadOperationQR(operNum: string) {
    const payload = {
      qrType: 'OPERATION',
      operNum: operNum
    };

    return this.http.post(
      `${this.baseUrl}/QRCode/GenerateQrWithOperation`, // use baseUrl here
      payload,
      { responseType: 'blob' } // return as Blob for download
    );
  }
  // Employee QR download
  downloadEmployeeQR(empCode: string) {
    const payload = {
      EmpNum: empCode,  // use the actual employee code
      QRType: 'Employee'
    };

    return this.http.post(
      `${this.baseUrl}/QRCode/GenerateQrWithEmployee`,
      payload,
      { responseType: 'blob' } // return as Blob for download
    );
  }
  // Machine QR download
  downloadMachineQR(machineNumber: string) {
    const payload = {
      machineNumber: machineNumber,
      qrType: 'MACHINE'
    };

    return this.http.post(
      `${this.baseUrl}/QRCode/GenerateQrWithMachine`,
      payload,
      { responseType: 'blob' } // return as Blob for download
    );
  }
  //Job QR Download
  GenerateQrWithJob(job: string) {
    const payload = {
      job: job,
      qrType: 'JOB'
    };

    return this.http.post(`${this.baseUrl}/QRCode/GenerateQrWithJob`, payload, {
      headers: this.getHeaders(),           
      responseType: 'blob',        
      withCredentials: true                  
    });
  }

  deleteEmployeeWC(empNum: string, wcCode: string) {
  return this.http.delete(
    `${this.baseUrl}/delete/DeleteEmployeeWC/${empNum}/${wcCode}`, {
      headers: this.getHeaders()
    }
  );
}

GetEmployee(): Observable<{ data: any[], total: number }> {
  return this.http.get<{ data: any[], total: number }>(`${this.baseUrl}/api/Employee/GetEmployeeList`);
}

// Add Employee-WC mapping
addEmployeeWc(payload: { wc: string; empNum: string; description?: string; name?: string }) {
  return this.http.post(
    `${this.baseUrl}/Post/AddEmployeeWc`,
    payload,
    {
      headers: this.getHeaders() // assuming you have a method to add auth headers
    }
  );
}


// Pause Single QC Job
pauseSingleQCJob(payload: any): Observable<any> {
  return this.http.post<any>(
    `${this.baseUrl}/Post/PauseSingleQCJob`,
    payload
  );
}

// Complete Single QC Job
completeSingleQCJob(payload: any): Observable<any> {
  return this.http.post<any>(
    `${this.baseUrl}/Post/CompleteSingleQCJob`,
    payload
  );
}

/** Get all completed QC jobs */
GetCompletedQCJobs(): Observable<{ data: any[], totalRecords: number }> {
  return this.http.get<{ data: any[], totalRecords: number }>(
    `${this.baseUrl}/Get/GetCompletedQCJobs`,
    { headers: this.getHeaders() }
  );
}

updateQCRemark(payload: any) {
  return this.http.post(`${this.baseUrl}/Post/UpdateQCRemark`, payload);
}

GetTransactionOverview(payload: any) {
  return this.http.post<any>(`${this.baseUrl}/Post/GetTransactionOverview`, payload);
}

GetTransactionData (payload: any) {
  return this.http.post(`${this.baseUrl}/Post/GetTransactionData`, payload);
}

GetEmployeeAndMachineStats() {
  return this.http.get(`${this.baseUrl}/Get/GetEmployeeAndMachineStats`);
}

markJobAsScrapped(payload: any): Observable<any> {
  return this.http.post(`${this.baseUrl}/Post/MarkJobScrapped`, payload);
}

getJobAsScrapped(){
  return this.http.get(`${this.baseUrl}/Get/GetScrappedQCJobs`);

}
}

