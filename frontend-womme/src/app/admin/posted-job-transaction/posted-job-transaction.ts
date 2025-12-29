import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject } from 'rxjs';
import { Table } from 'primeng/table';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import Swal from 'sweetalert2';
import { ActivatedRoute, Router } from '@angular/router';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';

export interface PostedTransaction {
  trans_num: string;
  shift?: string;
  job_rate?: any;
  total_dollar?: any;
  jobNumber: string;
  operationNumber: number;
  serialNumber: string;
  workCenter: string;
  employee_num?: string;
  employee_name?: string;
  machine_num?: string;
  machine_name?: string;
  status: string;
  start_time?: string;
  end_time?: string;
  total_hours?: number;
  trans_date?: string;
  timerHours?: number;
  allLogs?: PostedTransaction[];
}



@Component({
  selector: 'app-posted-job-transaction',
  templateUrl: './posted-job-transaction.html',
  styleUrls: ['./posted-job-transaction.scss'],
  standalone: true,
  imports: [
    CommonModule,
    HeaderComponent,
    SidenavComponent,
    TableModule,
    FormsModule,
    ButtonModule
  ]
})
export class PostedJobTransaction implements OnInit, AfterViewInit, OnDestroy {
  isSidebarHidden = false;
  transactions: PostedTransaction[] = [];
  searchTerm = '';
  totalRecords = 0;
  isLoading = false;
  expandedRows: { [key: string]: boolean } = {};
  roleId: number | null = null;
  employees: any[] = [];
  machines: any[] = [];
  editingRows: { [transNum: string]: PostedTransaction } = {};


  editingKey: string | null = null;

  @ViewChild('dt') dt!: Table;
  dtTrigger: Subject<any> = new Subject();

  constructor(
    private jobService: JobService,
    private router: Router,
    private route: ActivatedRoute,
    private loader: LoaderService
  ) {}

  ngOnInit(): void {
    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    this.roleId = userDetails.roleID || null;
    this.loadJobsLazy();
    this.loadEmployees();
    this.loadMachines();
  }

  ngAfterViewInit(): void {
    this.dtTrigger.next(true);
  }

  ngOnDestroy(): void {
    this.dtTrigger.unsubscribe();
  }

  // Lazy loading and filtering
  loadJobsLazy(event?: any) {
    this.isLoading = true;
    const page = event?.first ? event.first / event?.rows : 0;
    const size = event?.rows || 50;

    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const employeeCode = userDetails.employeeCode || '';
    const roleid = userDetails.roleID || '';

    this.loader.show();

    this.jobService.GetPostedTransactions(page, size, this.searchTerm)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any) => {
          let jobs = res.data;

          // Role-based filtering
          if (roleid === 4 || roleid === 5) {
            jobs = jobs.filter((job: any) => job.employee_num === employeeCode);
          }

          // Group by job/operation/serial/wc
          const grouped: { [key: string]: any[] } = jobs.reduce((acc: { [key: string]: any[] }, row: any) => {
            const key = `${row.jobNumber}|${row.operationNumber}|${row.serialNumber}|${row.workCenter}`;
            if (!acc[key]) acc[key] = [];
            acc[key].push(row);
            return acc;
          }, {});

          // Build final array
          this.transactions = Object.values(grouped).map((group: any[]) => { // <- type annotation added
            // Sort ascending by trans_date
           
            group.sort((a, b) => new Date(a.trans_date).getTime() - new Date(b.trans_date).getTime());

            // Prefer the completed row (status = 3) if it exists, else use the last one
            const completed = group.find(g => g.status === '3');
            const lastRow = completed || group[group.length - 1];
            const totalHours = group
            .filter(g => g.status === '3')      // all completed/OT rows
            .reduce((sum, g) => sum + (g.total_hours || 0), 0);

            return {
              ...lastRow,
              trans_num: lastRow.trans_num,
              timerHours: totalHours, // Already summed when status = 3
              allLogs: group
            };

          });

          this.totalRecords = this.transactions.length;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
        }
      });
  }

  // Search handler
  onSearchChange(term: string) {
    this.searchTerm = term;
    this.dt.reset(); // triggers lazy loading
  }

  // Sidebar toggle
  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  // Edit button handler
  onEdit(job: PostedTransaction) {
    // Navigate to edit page or show modal
    this.router.navigate(['/edit-job'], {
      queryParams: {
        jobNumber: job.jobNumber,
        serialNumber: job.serialNumber,
        operationNumber: job.operationNumber,
        workCenter: job.workCenter
      }
    });
  }

  // Convert timer hours to HH:mm:ss
  formatTimer(hours: number): string {
    const totalSeconds = Math.floor(hours * 3600);
    const h = Math.floor(totalSeconds / 3600);
    const m = Math.floor((totalSeconds % 3600) / 60);
    const s = totalSeconds % 60;
    return `${this.pad(h)}:${this.pad(m)}:${this.pad(s)}`;
  }

  pad(num: number): string {
    return num.toString().padStart(2, '0');
  }

  
  // Optional: get status text
  getJobStatus(logs: PostedTransaction[]): string {
    if (!logs || logs.length === 0) return '-';
    const lastLog = logs[logs.length - 1];
    return lastLog.status === '1' ? 'Started' :
           lastLog.status === '2' ? 'Paused' :
           lastLog.status === '3' ? 'Completed' : '-';
  }

  // component.ts
  formatDateFromString(iso?: string): string {
    if (!iso) return '-';
    const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (!m) return '-';
    const [, year, month, day] = m;
    return `${day}-${month}-${year.slice(2)}`;
  }

toggleRow(job: PostedTransaction) {
  const key = this.getRowKey(job); // use unique row key
  if (this.expandedRows[key]) {
    delete this.expandedRows[key];
  } else {
    this.expandedRows[key] = true;
  }
}

isRowExpanded(job: PostedTransaction): boolean {
  const key = this.getRowKey(job);
  return !!this.expandedRows[key];
}

getRowKey(job: PostedTransaction): string {
  // Use combination of jobNumber, serialNumber, operationNumber, workCenter
  return `${job.jobNumber}|${job.serialNumber}|${job.operationNumber}|${job.workCenter}`;
}



  exportJobs() {
    const wb = XLSX.utils.book_new();
    const sheetData: any[][] = [];

    // ----- Main Table Header -----
    sheetData.push([
      'S.No', 'Job', 'Serial No', 'Timer (HH:mm:ss)',
      'Opr No.', 'Emp Name', 'Machine Name', 'Work Center'
    ]);

    this.transactions.forEach((job, index) => {
      // ----- Main Row -----
      sheetData.push([
        index + 1,
        job.jobNumber,
        job.serialNumber,
        this.formatTimer(job.timerHours || job.total_hours || 0),
        job.operationNumber,
        job.employee_name,
        job.machine_name,
        job.workCenter
      ]);

      // ----- Nested Logs -----
      if (job.allLogs && job.allLogs.length > 0) {
        sheetData.push([]); // blank spacer row

        // Nested table header
        sheetData.push([
          'Trans Date',
          'Status',
          'Start Time',
          'End Time',
          'Hours',
          'Price',
          'Rate',
          'Shift',
          'Employee',
          'Machine'
        ]);

        job.allLogs.forEach(log => {
          sheetData.push([
            this.formatDateFromString(log.trans_date),
            this.getJobStatus([log]),
            this.formatTime(log.start_time),
            this.formatTime(log.end_time),
            this.formatTimer(log.total_hours || 0),
            log.total_dollar,
            log.job_rate,
            log.shift === '1' ? 'Day' : log.shift === '2' ? 'Overtime' : '',
            log.employee_name,
            log.machine_name
          ]);
        });

        sheetData.push([]); // blank row after nested table
      }
    });

    // ----- Create and Download Sheet -----
    const ws = XLSX.utils.aoa_to_sheet(sheetData);
    XLSX.utils.book_append_sheet(wb, ws, 'Jobs');

    const wbout = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
    saveAs(new Blob([wbout], { type: 'application/octet-stream' }), 'Jobs.xlsx');
  }

  formatTime(dateStr: any) {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }



  loadEmployees() {
    this.jobService.getAllEmployees()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any[]) => (this.employees = res),
        error: (err) => console.error('Failed to load employees list:', err)
      });
  }

  loadMachines() {
    this.jobService.getAllMachines()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any[]) => (this.machines = res),
        error: (err) => console.error('Failed to load machine list:', err)
      });   
  }

  getLogKey(log: PostedTransaction): string {
    return `${log.trans_num}|${log.serialNumber}|${log.operationNumber}|${log.workCenter}|${log.trans_date}|${log.status}`;
  }

  toMinutePrecision(dateTime?: string | Date): string {
    if (!dateTime) return '';

    const d = new Date(dateTime);

    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    const hours = String(d.getHours()).padStart(2, '0');
    const minutes = String(d.getMinutes()).padStart(2, '0');

    return `${year}-${month}-${day}T${hours}:${minutes}`;
  }



// Start editing a row
startEdit(log: PostedTransaction) {
  if (!log || log.trans_num == null) return;
  if (log.status?.toString() !== '3') return;

  this.editingRows[log.trans_num] = {
    trans_num: log.trans_num,
    jobNumber: log.jobNumber,
    serialNumber: log.serialNumber,
    operationNumber: log.operationNumber,
    workCenter: log.workCenter,
    start_time: this.toMinutePrecision(log.start_time),
    end_time: this.toMinutePrecision(log.end_time),
    status:log.status,
    employee_num: log.employee_num,
    machine_num: log.machine_num,
  } as PostedTransaction;
}


// Check if a row is being edited
isEditing(log: PostedTransaction): boolean {
  return !!this.editingRows[log.trans_num];
}



  saveEdit(log: PostedTransaction) {
    const row = this.editingRows[log.trans_num];
    if (!row) return;
    
    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const employeeCode = userDetails?.employeeCode;

    const payload = {
      TransNum: row.trans_num,               // ✅ Ensure correct TransNum
      SerialNumber: row.serialNumber,
      Job: row.jobNumber,
      OperationNumber: row.operationNumber,
      WorkCenter: row.workCenter,
      Status: row.status?.toString() ?? '',
      JobRate: row.job_rate,
      Shift: row.shift ?? '1',
      EmpNum: row.employee_num?.toString().trim() ?? '',
      MachineNum: row.machine_num?.toString().trim() ?? '',
      UpdatedBy: employeeCode, // or current user
      StartTime: row.start_time ?? null,
      EndTime: row.end_time ?? null
    };
    this.loader.show();
    this.jobService.updateJobLog(payload).pipe( finalize(() => { this.loader.hide(); })).subscribe({
      next: () => {
        Swal.fire({
          icon: 'success',
          title: 'Updated',
          text: 'Start & End time updated successfully',
          timer: 2000,
          showConfirmButton: false,
          toast: true,
          position: 'top-end'
        });

        delete this.editingRows[log.trans_num];
        this.loadJobsLazy();
      },
      error: () => {
        Swal.fire('Error', 'Failed to update time', 'error');
      }
    });
  }




calculateHoursAndAmount(log: any) {
  const row = this.editingRows[log.trans_num];
  if (!row) return;

  const startStr = row.start_time;
  const endStr = row.end_time;

  // 🧠 Guard: Only create Date if defined
  const start = startStr ? new Date(startStr) : null;
  const end = endStr ? new Date(endStr) : null;

  if (start && end && end > start) {
    const diffMs = end.getTime() - start.getTime();
    const diffHrs = diffMs / (1000 * 60 * 60);

    row.total_hours = parseFloat(diffHrs.toFixed(2));

    const rate = row.job_rate || 0;
    row.total_dollar = parseFloat((diffHrs * rate).toFixed(2));
  } else {
    row.total_hours = 0;
    row.total_dollar = 0;
  }
}





calculateAmount(log: PostedTransaction) {
  const row = this.editingRows[log.trans_num];
  if (row) {
    // Clamp total_hours to max 8
    let hours = Number(row.total_hours || 0);
    if (hours > 8) {
      hours = 8;
      row.total_hours = hours; // update the input value immediately
      Swal.fire({
        icon: 'warning',
        title: 'Maximum Hours Exceeded',
        text: 'Total hours cannot exceed 8.',
        timer: 2000,
        showConfirmButton: false
      });
    }

    const rate = Number(row.job_rate || 0);
    row.total_dollar = +(hours * rate).toFixed(2);
  }
}
deleteJobTransaction(job: any) {
  Swal.fire({
    title: 'Are you sure?',
    text: `Do you want to delete Job ${job.jobNumber}, Serial ${job.serialNumber} ,Operation ${job.operationNumber}?`,
    icon: 'warning',
    showCancelButton: true,
    confirmButtonText: 'Yes, delete it!',
    cancelButtonText: 'Cancel'
  }).then(result => {
    if (result.isConfirmed) {
      this.loader.show();
     this.jobService.deleteJobTransaction(job.jobNumber, job.serialNumber,job.operationNumber)
        .pipe( finalize(() => { this.loader.hide(); })).subscribe({
          next: (res) => {
            Swal.fire('Deleted!', res.message || 'Transaction has been deleted.', 'success');
            this.loadJobsLazy();
          },
          error: (err) => {
            Swal.fire('Error', err.error?.message || 'Failed to delete transaction', 'error');
          }
        });
    }
  });
}
 

}
