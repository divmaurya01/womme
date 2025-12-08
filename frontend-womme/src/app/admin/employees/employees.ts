import { Component, OnInit, ViewChild } from '@angular/core';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { FormsModule } from '@angular/forms';
import Swal from 'sweetalert2';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-employees',
  templateUrl: './employees.html',
  styleUrls: ['./employees.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    HeaderComponent,
    SidenavComponent,
    ButtonModule,
    TableModule,
    DialogModule,
    DropdownModule
  ]
})
export class EmployeesComponent implements OnInit {
  isSidebarHidden = false;
  employees: any[] = [];
  searchTerm = '';
  totalRecords = 0;
  isLoading = false;
  syncMessage: string | null = null;
  isError = false;
  roles: any[] = [];

  // Dialog / Form
  showForm = false;
  isEditMode = false;
  formError = '';
  newEmployee: any = {
    empNum: '',
    name: '',
    passwordHash: '',
    roleID: null,
    isActive: true,
    site_ref: 'DEFAULT',   
    createdBy: 'mahima', 
  };

  @ViewChild('employeeForm') employeeForm: any;

  constructor(private jobService: JobService, private loader: LoaderService) {}

  ngOnInit(): void {
    this.loadRoles();
    this.loadEmployeesLazy();
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  /** Map API employee -> frontend model */
  mapEmployee(apiEmp: any) {
    return {
      empNum: apiEmp.emp_num || apiEmp.empNum || '',
      name: apiEmp.name || '',
      passwordHash: apiEmp.passwordHash || '',
      roleID: apiEmp.roleID || apiEmp.role_id || null,
      isActive: apiEmp.isActive ?? apiEmp.is_active ?? true,
      site_ref: apiEmp.site_ref?.trim() || 'DEFAULT',
      createdBy: apiEmp.createdBy?.trim() || 'system admin',
    };
  }

  resetForm(isEdit: boolean = false) {
    if (!isEdit) {
      this.newEmployee = {
        empNum: '',
        name: '',
        passwordHash: '',
        roleID: null,
        isActive: true,
        site_ref: 'DEFAULT',
        createdBy: 'system admin'
      };
    }
    this.formError = '';
  }

  /** Load employees (with pagination) */
  loadEmployeesLazy(event?: any): void {
    this.isLoading = true;
    const page = event?.first ? event.first / event?.rows : 0;
    const size = event?.rows || 10;
    this.loader.show();
    this.jobService.GetEmployees(page, size, this.searchTerm)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res) => {
          this.employees = res.data.map((emp: any) => this.mapEmployee(emp));
          this.totalRecords = res.total;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
          Swal.fire('Error', 'Failed to load employees', 'error');
        }
      });
  }

  onSearchChange(value: string): void {
    this.searchTerm = value;
    this.loadEmployeesLazy();
  }

  /** Download QR */
  downloadEmployeeQR(empNum: string) {
    this.loader.show();
    this.jobService.downloadEmployeeQR(empNum)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe((blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `EMP-${empNum}-QR.png`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      }, error => {
        console.error("Error downloading QR:", error);
      });
  }

  openAddDialog() {
    this.resetForm();
    this.isEditMode = false;
    this.showForm = true;
    setTimeout(() => this.resetFormState(), 0);
  }

  openEditDialog(employee: any) {
    this.newEmployee = this.mapEmployee(employee);
    this.isEditMode = true;
    this.showForm = true;
    setTimeout(() => this.resetFormState(), 0);
  }

  resetFormState() {
    if (this.employeeForm) {
      this.employeeForm.resetForm(this.newEmployee); 
      this.employeeForm.form.markAsPristine();
      this.employeeForm.form.markAsUntouched();
    }
  }

  submitForm(form: any) {
    if (form.invalid) {
      this.formError = 'Please fill all required fields.';
      return;
    }

    this.newEmployee.site_ref = this.newEmployee.site_ref?.trim() || 'DEFAULT';
    this.newEmployee.createdBy = this.newEmployee.createdBy?.trim() || 'system admin';

    if (this.isEditMode) {
      this.loader.show();
      this.jobService.updateEmployee(this.newEmployee.empNum, this.newEmployee)
        .pipe(finalize(() => this.loader.hide()))
        .subscribe({
          next: () => {
            this.loadEmployeesLazy();
            this.showForm = false;
            Swal.fire('Updated', 'Employee updated successfully.', 'success');
          },
          error: (err) => {
            this.formError = err.error?.message || 'Error updating employee.';
          }
        });
    } else {
      this.loader.show();
      this.jobService.addEmployee(this.newEmployee)
        .pipe(finalize(() => this.loader.hide()))
        .subscribe({
          next: () => {
            this.loadEmployeesLazy();
            this.showForm = false;
            Swal.fire('Created', 'Employee created successfully.', 'success');
          },
          error: (err) => {
            this.formError = err.error?.message || 'Error creating employee.';
          }
        });
    }
  }

  onDialogClose() {
    this.resetForm();
    this.loadEmployeesLazy();
  }

  loadRoles() {
    this.loader.show();
    this.jobService.getAllRole()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res) => {
          this.roles = res.map(r => ({
            roleID: r.roleID,
            roleName: r.roleName
          }));
        },
        error: (err) => {
          console.error('Error loading roles', err);
        }
      });
  }
  syncEmployee() {
    this.isLoading = true;
    this.syncMessage = null;
    this.isError = false;
    this.loader.show();
    this.jobService.SyncEmployeeMst()
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (res: any) => {
        this.isLoading = false;
        this.syncMessage = res; // backend returns string
        this.isError = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.syncMessage = err.error || "Something went wrong while syncing.";
        this.isError = true;
      }
    });
  }
}
