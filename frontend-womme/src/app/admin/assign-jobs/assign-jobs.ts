import { Component, HostListener, OnInit } from '@angular/core';
import { JobService, AssignedJob } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { FormsModule } from '@angular/forms';
import { DropdownModule } from 'primeng/dropdown';
import { DialogModule } from 'primeng/dialog';
import Swal from 'sweetalert2';
import { ViewChild } from '@angular/core';
import { LoaderService } from '../../services/loader.service';
@Component({
  selector: 'app-assigned-jobs',
  templateUrl: './assign-jobs.html',
  styleUrls: ['./assign-jobs.scss'],
  imports:[HeaderComponent,SidenavComponent,DropdownModule,FormsModule,CommonModule,InputTextModule,ButtonModule,TableModule, DialogModule ]
})
export class AssignJobComponent implements OnInit {
  assignedJobs: AssignedJob[] = [];
  searchText = '';
showForm = false;
users: any[] = []; 
form = {
  employeeCode: '',
  jobNo: '',
  assignedHours: 0,
  remark: ''
};
formError: string = '';
jobOptions: any[] = [];
employees: any[] = [];
selectedEntryNo: number | null = null;
isSidebarHidden = window.innerWidth <= 1024;
@ViewChild('assignedJobForm') assignedJobForm: any;

  constructor(private jobService: JobService,private loader:LoaderService) {}

  ngOnInit(): void {
    this.checkScreenSize();
    this.loadJobs();
      this.loadUsers(); 
        //this.loadJobOptions();  
  }
openAddDialog(): void {
  this.showForm = true;
  this.resetForm(); // ✅ Reset form model
  if (this.assignedJobForm) {
    this.assignedJobForm.resetForm(); // ✅ Reset validation state
  }
}

closeForm(): void {
  this.showForm = false;
}


 @HostListener('window:resize')
  onResize() {
    this.checkScreenSize();
  }

    checkScreenSize() {
    if (window.innerWidth <= 1024) {
      this.isSidebarHidden = true;   // Mobile → hidden
    } else {
      this.isSidebarHidden = false;  // Desktop → visible
    }
  }
submitForm(): void {
  if (this.assignedJobForm.invalid) {
    this.assignedJobForm.control.markAllAsTouched(); 
    // if (this.form.assignedHours <= 0) {
    //   Swal.fire('Validation Error', 'Assigned hours must be greater than 0.', 'warning');
    // }
    return; 
  }

  const payload = {
    employeeCode: this.form.employeeCode,
    jobNumber: this.form.jobNo,
    assignedHours: this.form.assignedHours,
    remark: this.form.remark,
    updatedAt: this.jobService.getLocalDateTime(),
    createdAt: this.jobService.getLocalDateTime()
  };

  if (this.selectedEntryNo) {
    this.jobService.updateAssignedJob(this.selectedEntryNo, payload).subscribe({
      next: () => {
        Swal.fire('Success', 'Assigned job updated successfully', 'success');
        this.loadJobs();
        this.closeForm();
        this.resetForm();
      },
      error: (err) => {
      this.formError = err?.error?.message || 'Failed to update job. Please try again.';


      }
    });
  } else {
    this.jobService.createAssignedJob(payload).subscribe({
      next: () => {
        Swal.fire('Success', 'Assigned job created successfully', 'success');
        this.addEmployeeLog(this.form.employeeCode, this.form.jobNo);
        this.loadJobs();
        this.closeForm();
        this.resetForm();

      },
      error: (err) => {
               this.formError = err?.error?.message || 'Failed to assign job. Please try again.';

      }
    });
  }
}

addEmployeeLog(employeeCode: string, jobNumber: string): void {
  const logPayload = {
    jobNumber: jobNumber,
    statusID: 1, // Always 1 for new assignment
    employeeCode: employeeCode,
    statusTime: this.jobService.getLocalDateTime() // Current time
  };
  console.log('Sending EmployeeLog Payload:', logPayload); 

  this.jobService.addEmployeeLog(logPayload).subscribe({
    next: () => {
      console.log('Employee log added successfully.');
    },
    error: (err) => {
      console.error('Failed to add employee log:', err);
    }
  });
}


editAssignedJob(event:Event,job: AssignedJob): void {
    event.preventDefault(); 
  this.form = {
    employeeCode: job.employeeCode,
    jobNo: job.jobNumber,
    assignedHours: job.assignedHours,
    remark: job.remark
  };
  this.selectedEntryNo = job.entryNo;
  this.showForm = true;
}


resetForm(): void {
  this.form = {
    employeeCode: '',
    jobNo: '',
    assignedHours: 0,
    remark: ''
  };
    this.selectedEntryNo = null;
  if (this.assignedJobForm) {
    this.assignedJobForm.resetForm();  
 }
}

  loadJobs(): void {
    this.jobService.getAssignedJobs().subscribe({
      next: data => {
        console.log('Loaded jobs:', data); 
        this.assignedJobs = data;
      },
      error: err => console.error('Failed to load jobs:', err)
    });
  }

  // loadJobOptions(): void {
  //   this.jobService.GetJobs().subscribe({
  //     next: data => {
  //       this.jobOptions = data.map(job => ({
  //         label: job.jobName,         
  //         value: job.jobNumber            
  //       }));
  //     },
  //     error: err => console.error('Error loading job master:', err)
  //   });
  // }

  loadUsers(): void {
    this.jobService.UserMaster().subscribe({
      next: data => {
        this.users = data.map(user => ({
          label: user.userName,          
          value: user.employeeCode        
        }));
      },
      error: err => console.error('Failed to load users:', err)
    });
  }


  onEmployeeChange(event: any): void {
    console.log("👤 Selected Employee Code:", event.value);
  }

 
   

    toggleSidebar() {
      this.isSidebarHidden = !this.isSidebarHidden;
    }

  filterRow(job: AssignedJob): boolean {
    if (!this.searchText) return true;
    const term = this.searchText.toLowerCase();
    return Object.values(job).some(value =>
      value?.toString().toLowerCase().includes(term)
    );
  }

deleteAssignedJob(event: Event, entryNo: number): void {
  event.preventDefault();

  Swal.fire({
    title: 'Are you sure?',
    text: 'This assigned job will be permanently deleted!',
    icon: 'warning',
    showCancelButton: true,
    confirmButtonColor: '#d33',
    cancelButtonColor: '#3085d6',
    confirmButtonText: 'Yes, delete it!'
  }).then((result) => {
    if (result.isConfirmed) {
      // Show loading indicator
      Swal.fire({
        title: 'Deleting...',
        text: 'Please wait while the assigned job is being deleted.',
        allowOutsideClick: false,
        didOpen: () => {
          Swal.showLoading();
        }
      });

      this.jobService.deleteAssignedJob(entryNo).subscribe({
        next: () => {
          Swal.fire('Deleted!', 'Assigned job has been deleted successfully.', 'success');
          this.loadJobs(); // Reload jobs list
        },
        error: (err) => {
          console.error('Delete failed:', err);

          let errorMessage = 'Failed to delete assigned job. Please try again.';

          if (err.status === 0) {
            errorMessage = 'Cannot connect to the server. Please check your backend or network.';
          } else if (err.status === 404) {
            errorMessage = 'Assigned job not found. It may have already been deleted.';
          } else if (err.error?.message) {
            errorMessage = err.error.message;
          }

          Swal.fire('Error', errorMessage, 'error');
        }
      });
    }
  });
}



}
