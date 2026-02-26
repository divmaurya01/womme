import { Component, HostListener, OnInit } from '@angular/core';
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
import * as XLSX from 'xlsx';

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
 isSidebarHidden = window.innerWidth <= 1024;

  workCenters: any[] = [];
  employees: any[] = [];

  workCenterDropdown: any[] = [];

  
  filteredWorkCenters: any[] = [];   // 🔥 for table
  globalSearch = '';

  formError: string | null = null;
  isSubmitting = false;


  searchTerm = '';
  totalOperations = 0;
  isLoading = false;
  isOperationLoading = false;

  selectedEmployee: any = null;
  selectedWorkCenter: any = null;

  showAssignDialog = false;

  syncMessage = '';
  isError = false;

  constructor(private jobService: JobService, private loader: LoaderService) { }

  ngOnInit(): void {
    this.checkScreenSize();
    this.loadWorkCenters();
    this.loadEmployees();
    this.loadWorkCenterDropdown();
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
          this.loadWorkCenterDropdown();
        },
        error: () => {
          this.isLoading = false;
          this.syncMessage = 'Error syncing WorkCenter';
          this.isError = true;
        }
      });
  }

  loadWorkCenterDropdown(): void {
    this.jobService.GetWorkCenterMaster()
      .subscribe({
        next: (res) => {
          if (res && res.data) {
            this.workCenterDropdown = res.data.map((wc: any) => ({
              ...wc,
              displayText: `${wc.wc} (${wc.description})`
            }));
          }
        }
      });
  }


  // Load WorkCenters
  loadWorkCenters(): void {
  this.loader.show();

  this.jobService.GetWorkCenters(0, 100000, '')   // large size OR backend “all”
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (res) => {
        if (res && res.data) {
          this.workCenters = res.data.map((wc: any, index: number) => {
            const womFormatted = wc.womm_id
              ? `WME00${wc.womm_id.toString().padStart(3, '0')}`
              : '';

            return {
              entryNo: index + 1,
              wc: wc.wc,
              empNum: wc.empNum,
              description: wc.description,
              name: wc.name,
              womm_id: wc.womm_id,
              womSearch: womFormatted.toLowerCase() 
            };
          });

          this.filteredWorkCenters = [...this.workCenters];

        }
      }
    });
}


onGlobalSearch(): void {
  const rawSearch = this.globalSearch.toLowerCase().trim();

  if (!rawSearch) {
    this.filteredWorkCenters = [...this.workCenters];
    return;
  }

  const keywords = rawSearch
    .split('|')
    .map(k => k.trim())
    .filter(k => k.length > 0);

  this.filteredWorkCenters = this.workCenters.filter(wc =>
    keywords.some(keyword =>
      Object.values(wc).some(value =>
        value?.toString().toLowerCase().includes(keyword)
      )
    )
  );
}


formatWomMid(womm_id: number | null): string {
  if (!womm_id) return '-';
  return `WME00${womm_id.toString().padStart(3, '0')}`;
}




 loadEmployees(): void {
  this.jobService.getAllEmployees()
    .subscribe({
      next: (res) => {
        if (Array.isArray(res)) {
          this.employees = res.map((emp: any) => ({
            ...emp,
            displayText: `${emp.empName} (${emp.empNum})`
          }));
        } else {
          this.employees = [];
        }
      },
      error: (err) => {
        console.error('Error loading employees:', err);
        this.employees = [];
      }
    });
}


  // Assign Employee-WC
  assignEmployeeWC(): void {
      // Frontend validation
      if (!this.selectedEmployee || !this.selectedWorkCenter) {
        this.formError = 'Please select both Employee and WorkCenter.';
        return;
      }

      this.formError = null;
      this.isSubmitting = true;

      const payload = {
        empNum: this.selectedEmployee.empNum,
        wc: this.selectedWorkCenter.wc,
        name: this.selectedEmployee.empName,
        description: this.selectedWorkCenter.description,
      };

      this.loader.show();

      this.jobService.addEmployeeWc(payload)
        .pipe(finalize(() => {
          this.loader.hide();
          this.isSubmitting = false;
        }))
        .subscribe({
          next: () => {
            Swal.fire('Success', 'Employee-WC assigned successfully', 'success');

            this.showAssignDialog = false;
            this.selectedEmployee = null;
            this.selectedWorkCenter = null;
            this.formError = null;

            this.loadWorkCenters();
          },
          error: (err) => {
            // ✅ show backend error inside dialog
            this.formError =
              err?.error?.message ||
              err?.message ||
              'Failed to assign Employee to WorkCenter.';
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


  exportWorkCenterEmployeeToExcel(): void {
    if (!this.filteredWorkCenters || this.filteredWorkCenters.length === 0) {
      Swal.fire('No Data', 'No WorkCenter-Employee data available to export', 'info');
      return;
    }

    const exportData = this.filteredWorkCenters.map((wc, index) => ({
      'Sr No': index + 1,
      'Womme Id': wc.womm_id,
      'Work Center': wc.wc,
      'Employee No': wc.empNum,
      'Description': wc.description,
      'Employee Name': wc.name
      
    }));

    const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(exportData);
    const workbook: XLSX.WorkBook = {
      Sheets: { 'WorkCenter-Employee': worksheet },
      SheetNames: ['WorkCenter-Employee']
    };

    XLSX.writeFile(workbook, 'WorkCenter_Employee_Mapping.xlsx');
  }



}
