import { Component, OnInit } from '@angular/core';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { DialogModule } from 'primeng/dialog';
import Swal from 'sweetalert2';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
import * as XLSX from 'xlsx';


@Component({
  selector: 'app-machines',
  standalone: true,
  templateUrl: './machines.html',
  styleUrls: ['./machines.scss'],
  imports: [CommonModule, FormsModule, HeaderComponent, SidenavComponent, ButtonModule, TableModule, DialogModule]
})
export class MachineComponent implements OnInit {
  isSidebarHidden = false;
  showForm = false;
  machines: any[] = [];
  searchText = '';
  formError: string = '';

  filteredMachinesList: any[] = [];
  globalSearch = '';


  newMachine: any = {
    machineNumber: null,
    machineName: '',
    machineDescription: ''
  };
  form = {
    machineNumber: null,
    machineName: '',
    machineDescription: ''
  };


editingMachineEntryNo: number | null = null;

  constructor(private jobService: JobService,private loader:LoaderService) {}

  ngOnInit(): void {
    this.loadMachines();
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  loadMachines(): void {
  this.loader.show();
  this.jobService.getMachineMasters()
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (data) => {
        this.machines = data || [];
        this.filteredMachinesList = [...this.machines]; // 🔥 key line
      },
      error: (err) => console.error('Error loading machines:', err)
    });
}

onGlobalSearch(): void {
  const rawSearch = this.globalSearch.toLowerCase().trim();

  if (!rawSearch) {
    this.filteredMachinesList = [...this.machines];
    return;
  }

  // Split by | for multi-search
  const keywords = rawSearch
    .split('|')
    .map(k => k.trim())
    .filter(k => k.length > 0);

  this.filteredMachinesList = this.machines.filter(machine =>
    keywords.some(keyword =>
      Object.values(machine).some(value =>
        value?.toString().toLowerCase().includes(keyword)
      )
    )
  );
}


exportMachinesToExcel(): void {
  if (!this.filteredMachinesList || this.filteredMachinesList.length === 0) {
    Swal.fire('No Data', 'No machines available to export', 'info');
    return;
  }

  // Prepare clean export data
  const exportData = this.filteredMachinesList.map((m, index) => ({
    'Sr No': index + 1,
    'Machine Number': m.machineNumber,
    'Machine Name': m.machineName,
    'Description': m.machineDescription
  }));

  // Create worksheet & workbook
  const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(exportData);
  const workbook: XLSX.WorkBook = {
    Sheets: { Machines: worksheet },
    SheetNames: ['Machines']
  };

  // Export file
  XLSX.writeFile(workbook, 'Machines_List.xlsx');
}




  openAddDialog(form:any): void {
      this.resetForm(); // Clear machine data
  if (form) {
    form.resetForm(); // Reset Angular validation state
  }
    this.showForm = true;
  }

  closeForm(): void {
    this.showForm = false;
    console.log('Machine form closed');
  }
allowOnlyNumbers(event: KeyboardEvent): void {
  const charCode = event.charCode;
  if (charCode < 48 || charCode > 57) {
    event.preventDefault();
  }
}
  editMachine(event: Event, machine: any): void {
    event.preventDefault(); // Prevent page reload
    
    this.editingMachineEntryNo = machine.entryNo;
    this.newMachine = {
      machineNumber: machine.machineNumber,
      machineName: machine.machineName,
      machineDescription: machine.machineDescription
    };
    this.showForm = true;
  }


  submitForm(machineForm: any): void {
      this.formError = ''; // Reset error message before submit
   if (machineForm.invalid) {
    Object.keys(machineForm.controls).forEach(field => {
      const control = machineForm.controls[field];
      control.markAsTouched({ onlySelf: true });
    });
    return; // Stop submission, show errors
  }

  const payload: any = {
    machineNumber: this.newMachine.machineNumber?.toString().trim(),  // backend expects `machine`
    machineName: this.newMachine.machineName.trim(),
    machineDescription: this.newMachine.machineDescription.trim(),
    updatedAt: this.jobService.getLocalDateTime()
  };

  if (this.editingMachineEntryNo !== null) {
    this.loader.show();
    this.jobService.updateMachineMaster(this.editingMachineEntryNo, payload)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: () => {
        Swal.fire('Success', 'Machine updated successfully.', 'success');
        this.loadMachines();
        this.resetForm();
        this.closeForm();
      },
      error: (err) => {
       this.formError = err?.error?.message || err?.message || 'Failed to update machine.';

      }
    });
  } else {
    this.loader.show();
    this.jobService.createMachine(payload)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: () => {
        Swal.fire('Success', 'Machine created successfully.', 'success');
        this.loadMachines();
        this.resetForm();
        this.closeForm();
      },
      error: (err) => {
               this.formError = err?.error?.message || err?.message || 'Failed to create machine.';

      }
    });
  }
}



resetForm(): void {
  this.newMachine = {
    entryNo: null,
    machineNumber: null,
    machineName: '',
    machineDescription: ''
  };
  this.editingMachineEntryNo = null;
  this.showForm = false;
}



deleteMachine(event: Event, entryNo: number): void {
  event.preventDefault();

  Swal.fire({
    title: 'Are you sure?',
    text: 'This machine will be permanently deleted!',
    icon: 'warning',
    showCancelButton: true,
    confirmButtonColor: '#d33',
    cancelButtonColor: '#3085d6',
    confirmButtonText: 'Yes, delete it!'
  }).then((result) => {
    if (result.isConfirmed) {
      // Show loading
      Swal.fire({
        title: 'Deleting...',
        text: 'Please wait while the machine is being deleted.',
        allowOutsideClick: false,
        didOpen: () => {
          Swal.showLoading();
        }
      });
      this.loader.show();
      this.jobService.deleteMachineMaster(entryNo)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          Swal.fire('Deleted!', 'Machine has been deleted successfully.', 'success');
          this.loadMachines();
        },
        error: (err) => {
          console.error('Delete failed:', err);

          let errorMessage = 'Machine could not be deleted.';

          if (err.status === 0) {
            errorMessage = 'Cannot connect to the server. Please check your network or backend.';
          } else if (err.status === 404) {
            errorMessage = 'Machine not found. It may have already been deleted.';
          } else if (err.error?.message) {
            errorMessage = err.error.message;
          }

          Swal.fire('Error', errorMessage, 'error');
        }
      });
    }
  });
}



downloadMachineQR(event: Event, machineNumber: any) {
  event.preventDefault();

  const payload = {
    machineNumber: machineNumber?.toString(), // force string
    qrType: 'MACHINE'
  };
  this.loader.show();
  this.jobService.downloadMachineQR(payload.machineNumber)
  .pipe(finalize(() => this.loader.hide()))
  .subscribe({
    next: (blob: Blob) => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `QR_Machine_${payload.machineNumber}.png`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    },
    error: (err) => {
      console.error('Failed to download Machine QR:', err);
      Swal.fire({
        icon: 'error',
        title: 'Error',
        text: 'Failed to download Machine QR. Please try again.'
      });
    }
  });
}



}
