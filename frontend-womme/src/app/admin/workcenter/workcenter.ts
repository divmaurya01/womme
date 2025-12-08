import { Component, OnInit } from '@angular/core';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { CommonModule, NgIf } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { FormsModule } from '@angular/forms';
import Swal from 'sweetalert2';
import { DialogModule } from 'primeng/dialog';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
import { DropdownModule } from 'primeng/dropdown';

@Component({
  selector: 'app-workcenter',
  templateUrl: './workcenter.html',
  styleUrls: ['./workcenter.scss'],
  standalone: true,
  imports: [
    CommonModule,
     NgIf,
    FormsModule,
    HeaderComponent,
    SidenavComponent,
    ButtonModule,
    TableModule,
    DialogModule,
    DropdownModule
  ]
})
export class workcenter implements OnInit {
  isSidebarHidden = false;

  workCenters: any[] = [];
  employees: any[] = [];

  workCenterDropdown: any[] = [];

  searchTerm = '';
  totalOperations = 0;
  isLoading = false;
  isOperationLoading = false;

  selectedEmployee: any = null;
  selectedWorkCenter: any = null;

  showAssignDialog = false;

  syncMessage = '';
  isError = false;

  constructor(private jobService: JobService, private loader: LoaderService) {}

  ngOnInit(): void {
    this.loadWorkCenters();
    this.loadEmployees();
   // this.loadWorkCenterDropdown();
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  onSearchChange(value: string): void {
    this.searchTerm = value;
    this.loadWorkCenters();
  }

  // Sync WorkCenters
  SyncWcMst(): void {
    this.isLoading = true;
    this.loader.show();
    this.jobService.SyncWcMst()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          this.isLoading = false;
          this.syncMessage = 'WorkCenter synced successfully';
          this.isError = false;
          this.loadWorkCenters();
        //  this.loadWorkCenterDropdown();
        },
        error: () => {
          this.isLoading = false;
          this.syncMessage = 'Error syncing WorkCenter';
          this.isError = true;
        }
      });
  }

  // Load table WorkCenters
 // Load WorkCenters
// Load WorkCenters
loadWorkCenters(event?: any): void {
  this.isOperationLoading = true;
  const page = event?.first ? event.first / (event?.rows || 50) : 0;
  const size = event?.rows || 50;

  this.loader.show();

  this.jobService.GetWorkCenters(page, size, this.searchTerm)
    .pipe(finalize(() => {
      this.loader.hide();
      this.isOperationLoading = false;
    }))
    .subscribe({
      next: (res) => {
        if (res && res.data) {
          // Table data
          this.workCenters = res.data.map((wc: any, index: number) => ({
            entryNo: page * size + index + 1,
            wc: wc.wc,
            empNum: wc.empNum,
            description: wc.description,
            name: wc.name
          }));

          // ✅ Dropdown data (like employees)
          this.workCenterDropdown = res.data.map((wc: any) => ({
            ...wc,
            displayText: `${wc.wc} (${wc.description})`
          }));

          this.totalOperations = res.total || 0;
        } else {
          this.workCenters = [];
          this.workCenterDropdown = [];
          this.totalOperations = 0;
        }
      },
      error: (err) => {
        Swal.fire('Error', 'Failed to load work centers', 'error');
        console.error('Error loading work centers:', err);
      }
    });
}



  // Load Employee list
  loadEmployees(): void {
    this.jobService.GetEmployees()
      .subscribe({
        next: (res) => {
          if (res && res.data) {
            this.employees = res.data.map((emp: any) => ({
              ...emp,
              displayText: `${emp.name} (${emp.emp_num})`
            }));
          } else {
            this.employees = [];
          }
        },
        error: (err) => {
          console.error('Error loading employees:', err);
        }
      });
  }

  // Assign Employee-WC
 assignEmployeeWC(): void {
  if (!this.selectedEmployee || !this.selectedWorkCenter) {
    Swal.fire('Validation', 'Please select both Employee and WorkCenter', 'warning');
    return;
  }

  const payload = {
    empNum: this.selectedEmployee.emp_num,   // ✅ correct
    wc: this.selectedWorkCenter.wc,          // ✅ correct
    name: this.selectedEmployee.name,        // ✅ correct
    description: this.selectedWorkCenter.description
  };

  this.loader.show();
  this.jobService.addEmployeeWc(payload)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: () => {
        Swal.fire('Success', 'Employee-WC assigned successfully', 'success');
        this.showAssignDialog = false;
        this.selectedEmployee = null;       // ✅ clear selection
        this.selectedWorkCenter = null;     // ✅ clear selection
        this.loadWorkCenters();
      },
      error: (err) => {
        Swal.fire('Error', 'Failed to assign Employee-WC', 'error');
        console.error('Error assigning Employee-WC:', err);
      }
    });
}


  // Delete Employee-WC mapping
  deleteEmployeeWC(empNum: string, wc: string): void {
    Swal.fire({
      title: 'Are you sure?',
      text: `Delete mapping for Employee ${empNum} - WC ${wc}?`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Yes, Delete',
      cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {
        this.loader.show();
        this.jobService.deleteEmployeeWC(empNum, wc)
          .pipe(finalize(() => this.loader.hide()))
          .subscribe({
            next: () => {
              Swal.fire('Deleted!', 'Mapping deleted successfully', 'success');
              this.loadWorkCenters();
            },
            error: (err) => {
              Swal.fire('Error', 'Failed to delete mapping', 'error');
              console.error('Error deleting mapping:', err);
            }
          });
      }
    });
  }
}
