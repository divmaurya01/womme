import { Component, OnInit } from '@angular/core';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import Swal from 'sweetalert2';
import { JobService } from '../../services/job.service';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';

interface WorkCenter {
  wcName: string;
  wcCode: string;
}

interface Machine {
  machineNumber: string;       // matches API field
  machineName: string;         // matches API field
  wc?: string;
  wcName?: string;
}


@Component({
  selector: 'app-machine-employee',
  templateUrl: './machine-employee.html',
  styleUrls: ['./machine-employee.scss'],
  imports: [
    HeaderComponent,
    SidenavComponent,
    CommonModule,
    TableModule,
    ButtonModule,
    FormsModule,
    DialogModule
  ]
})
export class MachineEmployeeComponent implements OnInit {
  showForm = false;
  formError: string | null = null;
  isSidebarHidden = false;

  machines: any[] = [];       // table data
  machinesList: Machine[] = [];   // machine dropdown
  wcList: WorkCenter[] = [];      // WC dropdown

  selectedWc: WorkCenter | null = null;

  newMachine: any = {
    machineNumber: '',
    machineDescription: '',
    wc: '',
    wcName: ''
  };

  constructor(private jobService: JobService, private loader: LoaderService) {}

  ngOnInit() {
    this.loadMachines();
    this.loadMachinesList();
    this.loadWcList();
  }

 /** Fetch machine table */
loadMachines() {
  this.loader.show();
  this.jobService.GetAllWcMachines()
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (res: any[]) => {
        console.log('Raw API Response (GetAllWcMachines):', res);

        this.machines = res.map((m, index) => {
          const machine = {
            machineNumber: m.machineId,
            wc: m.wc,
            machineDescription: m.machineDescription,
            wcName: m.wcName || '(No Name)'
          };
          console.log(`Mapped machine [${index}]:`, machine);
          return machine;
        });

        console.log('Final machines array:', this.machines);
      },
      error: (err) => {
        console.error('Failed to load machines:', err);
      }
    });
}

/** Load machines for dropdown */
loadMachinesList() {
  this.loader.show();
  this.jobService.getAllMachines()
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (res: any[]) => {
        this.machinesList = res.map((m, index) => ({
          machineNumber: m.machineNumber,
          machineName: m.machineName,
          wc: m.wc,             // optional
          wcName: m.wcName || '(No Name)'
        }));
        console.log('Mapped machinesList:', this.machinesList);
      },
      error: (err) => console.error('Failed to load machine list:', err)
    });
}


  /** Load Work Centers for dropdown */
  loadWcList() {
    this.loader.show();
    this.jobService.GetAllWcMachines()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any[]) => {
          const distinctWcs = res
            .map(m => ({ wcCode: m.wc, wcName: m.wcName || '(No Name)' }))
            .filter((v, i, a) => a.findIndex(t => t.wcCode === v.wcCode) === i);
          this.wcList = distinctWcs;
        },
        error: (err) => console.error('Failed to load WCs:', err)
      });
  }

  /** When machine is selected */
selectedMachine: Machine | null = null;

onMachineSelect() {
  if (this.selectedMachine) {
    this.newMachine.machineNumber = this.selectedMachine.machineNumber;
    this.newMachine.machineDescription = this.selectedMachine.machineName; // Use machineName

    console.log('Selected Machine object:', this.selectedMachine);
    console.log('Machine Number:', this.selectedMachine.machineNumber);
    console.log('Machine Name:', this.selectedMachine.machineName);
  }
}

  /** When WC is selected */
  onWcSelect() {
    if (this.selectedWc) {
      this.newMachine.wc = this.selectedWc.wcCode;
      this.newMachine.wcName = this.selectedWc.wcName;
    }
  }

  toggleSidebar() {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  /** Open Add Form */
  openAddDialog() {
    this.resetForm();
    this.showForm = true;
  }

  /** Reset form */
  resetForm() {
    this.newMachine = {
      machineNumber: '',
      machineDescription: '',
      wc: '',
      wcName: ''
    };
    this.selectedMachine = null;
    this.selectedWc = null;
    this.formError = null;
  }

  /** Add Machine-WC mapping */
  submitForm(form: any) {
    if (form.invalid) {
      this.formError = 'Please fill all required fields correctly.';
      return;
    }

    this.formError = null;

    this.loader.show();
    this.jobService.AddMachineWc(this.newMachine)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          Swal.fire('Success!', 'Machine-WC added successfully.', 'success');
          this.showForm = false;
          this.resetForm();
          this.loadMachines();
        },
        error: (err) => {
          const msg = err.error?.message || 'Failed to add mapping. Please try again.';
          this.formError = msg;
        }
      });
  }

  /** Delete Machine-WC mapping */
  deleteMachineWC(event: Event, machineNumber: string, wcCode: string) {
    event.preventDefault();

    Swal.fire({
      title: 'Are you sure?',
      text: `Delete Machine (${machineNumber}) from WC ${wcCode}?`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Yes, delete it!',
      cancelButtonText: 'Cancel'
    }).then(result => {
      if (result.isConfirmed) {
        this.loader.show();
        this.jobService.deleteMachinewc(machineNumber, wcCode)
          .pipe(finalize(() => this.loader.hide()))
          .subscribe({
            next: () => {
              Swal.fire('Deleted!', 'Machine-WC mapping deleted successfully.', 'success');
              this.loadMachines();
            },
            error: (err) => {
              console.error('Error deleting:', err);
              Swal.fire('Error!', err);
            }
          });
      }
    });
  }
}
