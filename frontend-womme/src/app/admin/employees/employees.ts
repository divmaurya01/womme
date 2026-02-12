import { Component, HostListener, OnInit, ViewChild } from '@angular/core';
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
import * as XLSX from 'xlsx';


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
 isSidebarHidden = window.innerWidth <= 1024;
  employees: any[] = [];
  searchTerm = '';
  totalRecords = 0;
  isLoading = false;
  syncMessage: string | null = null;
  isError = false;
  roles: any[] = [];

  allEmployees: any[] = [];        // original full list
  filteredEmployees: any[] = [];  // table binding
  globalSearch = '';

  // Dialog / Form
  showForm = false;
  isEditMode = false;
  formError = '';
  newEmployee: any = {
    empNum: '',
    name: '',
    email: '',
    passwordHash: '',
    roleID: null,
    isActive: true,
    site_ref: 'DEFAULT',   
    createdBy: 'System Admin', 
  };

  @ViewChild('employeeForm') employeeForm: any;

  constructor(private jobService: JobService, private loader: LoaderService) {}

  ngOnInit(): void {
    this,this.checkScreenSize();
    this.loadRoles();
    this.loadEmployees();
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

  /** Map API employee -> frontend model */
  

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
 loadEmployees(): void {
  this.isLoading = true;
  this.loader.show();

  this.jobService.GetEmployees(0, 100000, '')
    .pipe(finalize(() => {
      this.loader.hide();
      this.isLoading = false;
    }))
    .subscribe({
      next: (res) => {
        this.allEmployees = res.data.map((emp: any) =>
          this.mapEmployee(emp)
        );

        this.filteredEmployees = [...this.allEmployees];
      },
      error: () => {
        Swal.fire('Error', 'Failed to load employees', 'error');
      }
    });
}

mapEmployee(apiEmp: any) {
  return {
    empNum: apiEmp.emp_num || '',
    name: apiEmp.name || '',
    email: apiEmp.email || '',  
    passwordHash: apiEmp.passwordHash || '',
    dept: apiEmp.dept || '',
    emp_type: apiEmp.emp_type || '',
    pay_freq: apiEmp.pay_freq || '',

    mfg_dt_rate: apiEmp.mfg_dt_rate ?? 0,
    mfg_ot_rate: apiEmp.mfg_ot_rate ?? 0,
    mfg_reg_rate: apiEmp.mfg_reg_rate ?? 0,

    hire_date: apiEmp.hire_date || null,
    roleID: apiEmp.roleID || null,
    isActive: apiEmp.isActive ?? true,
    site_ref: apiEmp.site_ref || 'DEFAULT',
    profileImage: apiEmp.profileImage || null,
    womm_id: apiEmp.womm_id || 0,

    loginID: `WME00${apiEmp.womm_id ?? ''}`

  };
}



onGlobalSearch(): void {
  const rawSearch = this.globalSearch.toLowerCase().trim();

  if (!rawSearch) {
    this.filteredEmployees = [...this.allEmployees];
    return;
  }

  const keywords = rawSearch
    .split('|')
    .map(k => k.trim())
    .filter(k => k.length > 0);

  this.filteredEmployees = this.allEmployees.filter(emp =>
    keywords.some(keyword =>
      Object.values(emp).some(value =>
        value?.toString().toLowerCase().includes(keyword)
      )
    )
  );
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
  this.isEditMode = true;

  this.newEmployee = {
    ...employee,         
    empNum: employee.empNum
  };

  this.showForm = true;

  setTimeout(() => {
    if (this.employeeForm) {
      this.employeeForm.form.markAsPristine();
      this.employeeForm.form.markAsUntouched();
    }
  }, 0);
}


  resetFormState() {
    if (this.employeeForm) {
      this.employeeForm.form.markAsPristine();
      this.employeeForm.form.markAsUntouched();
    }
  }

  private buildEmployeePayload() {
    return {
      empNum: this.newEmployee.empNum,
      name: this.newEmployee.name,
      email: this.newEmployee.email,
      passwordHash: this.newEmployee.passwordHash,
      roleID: this.newEmployee.roleID,
      createdBy: this.newEmployee.createdBy,
      isActive: this.newEmployee.isActive,
      loginID: this.newEmployee.loginID
    };
  }


  submitForm(form: any) {
    if (form.invalid) {
      this.formError = 'Please fill all required fields.';
      return;
    }

    // normalize values
    this.newEmployee.createdBy =
      this.newEmployee.createdBy?.trim() || 'system admin';

    const payload = this.buildEmployeePayload(); // 🔥 ONLY REQUIRED FIELDS

    if (this.isEditMode) {
      this.loader.show();
      this.jobService.updateEmployee(payload.empNum, payload)
        .pipe(finalize(() => this.loader.hide()))
        .subscribe({
          next: () => {
            this.showForm = false;
            Swal.fire('Updated', 'Employee updated successfully.', 'success');
            this.loadEmployees();
          },
          error: (err) => {
            this.formError = err.error?.message || 'Error updating employee.';
          }
        });
    } else {
      this.loader.show();
      this.jobService.addEmployee(payload)
        .pipe(finalize(() => this.loader.hide()))
        .subscribe({
          next: () => {
            this.showForm = false;
            Swal.fire('Created', 'Employee created successfully.', 'success');
            this.loadEmployees();
          },
          error: (err) => {
            this.formError = err.error?.message || 'Error creating employee.';
          }
        });
    }
  }


  onDialogClose() {
    this.resetForm();
    
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


  exportEmployeesToExcel(): void {
    if (!this.filteredEmployees || this.filteredEmployees.length === 0) {
      Swal.fire('No Data', 'No Employees available to export', 'info');
      return;
    }

    const exportData = this.filteredEmployees.map((emp, index) => ({
      'Sr No': index + 1,
      'Login ID': emp.loginID,
      'Employee No': emp.empNum,
      'Name': emp.name,
      'Email': emp.email, 
      'Department': emp.dept,
      'Employee Type': emp.emp_type,
      'Pay Frequency': emp.pay_freq,
      'Regular Rate': emp.mfg_reg_rate,
      'OT Rate': emp.mfg_ot_rate,
      'Double Rate': emp.mfg_dt_rate
    }));

    const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(exportData);
    const workbook: XLSX.WorkBook = {
      Sheets: { 'Employees': worksheet },
      SheetNames: ['Employees']
    };

    XLSX.writeFile(workbook, 'Employees_List.xlsx');
  }


}
