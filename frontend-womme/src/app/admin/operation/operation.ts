import { Component, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import Swal from 'sweetalert2';
import { TimeoutError } from 'rxjs';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
import * as XLSX from 'xlsx';

@Component({
  selector: 'app-operation-masters',
  templateUrl: './operation.html',
  styleUrls: ['./operation.scss'],
  standalone: true,
  imports: [CommonModule, FormsModule, HeaderComponent, SidenavComponent, TableModule, DialogModule]
})
export class OperationMastersComponent implements OnInit {
  isSidebarHidden = window.innerWidth <= 1024;
  showForm = false;
  operations: any[] = [];
  searchText = '';
  allOperations: any[] = [];        // full list
  filteredOperations: any[] = [];  // table binding
  globalSearch = '';

  formError: string = '';
  totalOperations = 0;
  isOperationLoading = false;
  operationSearchTerm = '';
  form = {
    entryNo: 0,
    operationNumber: 0,
    operationName: '',
    operationDescription: '',
    updatedAt: ''
  };

  constructor(private jobService: JobService,private loader:LoaderService) {}

  ngOnInit(): void {
    this.checkScreenSize();
    this.loadOperations();
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

 loadOperations(): void {
  this.isOperationLoading = true;
  this.loader.show();

  // Load ALL data at once
  this.jobService.GetDistinctOperations(0, 100000, '')
    .pipe(finalize(() => {
      this.loader.hide();
      this.isOperationLoading = false;
    }))
    .subscribe({
      next: (res) => {
        this.allOperations = res.data.map((num: number, index: number) => ({
          entryNo: index + 1,
          operationNumber: num
        }));

        // bind initially
        this.filteredOperations = [...this.allOperations];
      },
      error: (err) => {
        Swal.fire('Error', 'Failed to load operations', 'error');
        console.error('Error loading operations:', err);
      }
    });
}


onOperationSearchChange(value: string): void {
  const search = value.toLowerCase().trim();

  if (!search) {
    this.filteredOperations = [...this.allOperations];
    return;
  }

  this.filteredOperations = this.allOperations.filter(op =>
    Object.values(op).some(v =>
      v?.toString().toLowerCase().includes(search)
    )
  );
}




  openAddDialog(form?:any): void {
   
    this.showForm = true;
  }

  closeForm(): void {
    this.showForm = false;
  }

  downloadOperationQR(operation: string) {
    this.loader.show();
    this.jobService.downloadOperationQR(operation)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe((blob: Blob) => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `Operation-${operation }-QR.png`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    }, error => {
      console.error("Error downloading QR:", error);
    });
  }

  


allowOnlyNumbers(event: KeyboardEvent): void {
  const charCode = event.charCode;
  if (charCode < 48 || charCode > 57) {
    event.preventDefault();
  }
}

exportOperationsToExcel(): void {
  if (!this.filteredOperations || this.filteredOperations.length === 0) {
    Swal.fire('No Data', 'No Operations available to export', 'info');
    return;
  }

  const exportData = this.filteredOperations.map((op, index) => ({
    'Sr No': index + 1,
    'Operation Number': op.operationNumber
  }));

  const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(exportData);
  const workbook: XLSX.WorkBook = {
    Sheets: { 'Operations': worksheet },
    SheetNames: ['Operations']
  };

  XLSX.writeFile(workbook, 'Operations_List.xlsx');
}

  

}
