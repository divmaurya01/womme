import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { JobService } from '../../services/job.service';
import Swal from 'sweetalert2';
import { HttpClient } from '@angular/common/http';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';

interface ItemForm {
  entryNo: number;
  itemNumber: number;
  itemName: string;
  itemDescription: string;
}

@Component({
  selector: 'app-item-masters',
  templateUrl: './item.html',
  styleUrls: ['./item.scss'],
  standalone: true,
  imports: [
    HeaderComponent,
    SidenavComponent,
    CommonModule,
    FormsModule,
    TableModule,
    InputTextModule,
    DialogModule,
    ButtonModule
  ]
})
export class ItemComponent implements OnInit {
  items: any[] = [];
  searchTerm: string = '';
  isSidebarHidden = false;
  showForm = false;
  showAddItemDialog = false;
  editingItemId: number | null = null;
  totalRecords: number = 0;
  isLoading: boolean = false;

  form: ItemForm = this.initForm();
  newItem = {
    itemCode: '',
    itemName: '',
    description: '',
    status: 1
  };
  formError: string = '';

  constructor(
    private jobService: JobService,
    private http: HttpClient,
    private loader: LoaderService
  ) {}

  ngOnInit(): void {
    this.loadJobsLazy();
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  initForm(): ItemForm {
    return {
      entryNo: 0,
      itemNumber: 0,
      itemName: '',
      itemDescription: ''
    };
  }

  loadJobsLazy(event?: any): void {
    this.isLoading = true;
    const page = event?.first ? event.first / event?.rows : 0;
    const size = event?.rows || 5000;
    this.loader.show();
    this.jobService
      .GetItems(page, size, this.searchTerm)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res) => {
          this.items = res.data;
          this.totalRecords = res.total;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
          Swal.fire('Error', 'Failed to load items', 'error');
        }
      });
  }

  onSearchChange(value: string): void {
    this.searchTerm = value;
    this.loadJobsLazy(); // reload table when search changes
  }
}
