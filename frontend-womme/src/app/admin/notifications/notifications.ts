import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { FormsModule } from '@angular/forms';
import Swal from 'sweetalert2';
import { finalize } from 'rxjs/operators';

import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { JobService } from '../../services/job.service';
import { LoaderService } from '../../services/loader.service';

@Component({
  selector: 'app-notifications',
  standalone: true,
  templateUrl: './notifications.html',
  styleUrls: ['./notifications.scss'],
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    DialogModule,
    HeaderComponent,
    SidenavComponent
  ]
})
export class NotificationComponent implements OnInit {
  isSidebarHidden = false;
  notifications: any[] = [];
  filteredNotifications: any[] = [];
  globalSearch = '';

  showReplyDialog = false;
  selectedNotification: any = null;

  replyForm = {
    responseSubject: '',
    responseBody: ''
  };

  constructor(
    private jobService: JobService,
    private loader: LoaderService
  ) {}

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.loader.show();
    this.jobService.getNotifications()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (data) => {
          this.notifications = data || [];
          this.filteredNotifications = [...this.notifications];
        },
        error: (err) => {
          console.error(err);
          Swal.fire('Error', 'Failed to load notifications', 'error');
        }
      });
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }


  onGlobalSearch(): void {
  const rawSearch = this.globalSearch.toLowerCase().trim();

  if (!rawSearch) {
    this.filteredNotifications = [...this.notifications];
    return;
  }

  const keywords = rawSearch
    .split('|')
    .map(k => k.trim())
    .filter(k => k.length > 0);

  this.filteredNotifications = this.notifications.filter(notification =>
    keywords.some(keyword =>
      Object.values(notification).some(value =>
        value?.toString().toLowerCase().includes(keyword)
      )
    )
  );
}


  openReplyDialog(notification: any): void {
    this.selectedNotification = notification;
    this.replyForm = {
      responseSubject: '',
      responseBody: ''
    };
    this.showReplyDialog = true;
  }

 sendReply(): void {
  if (!this.replyForm.responseSubject || !this.replyForm.responseBody) {
    Swal.fire('Error', 'Subject and message are required', 'warning');
    return;
  }

  if (!this.selectedNotification?.notificationID) {
    Swal.fire('Error', 'Invalid notification selected', 'error');
    return;
  }

  const payload = {
    notificationID: this.selectedNotification.notificationID,
    responseSubject: this.replyForm.responseSubject,
    responseBody: this.replyForm.responseBody
  };

  this.loader.show();
  this.jobService
    .respondToNotification(payload)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: () => {
        Swal.fire('Success', 'Response sent successfully', 'success');
        this.showReplyDialog = false;
        this.loadNotifications();
      },
      error: (err) => {
        console.error(err);
        Swal.fire('Error', 'Failed to send response', 'error');
      }
    });
}



  closeDialog(): void {
    this.showReplyDialog = false;
  }
}
