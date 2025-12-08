import { Component, Output, EventEmitter, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JobService } from '../../services/job.service';
import { Router, NavigationEnd } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { ButtonModule } from 'primeng/button';
import { OverlayPanelModule } from 'primeng/overlaypanel';
import { LoaderService } from '../../services/loader.service';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, DropdownModule, ButtonModule, OverlayPanelModule, RouterModule],
  templateUrl: './header.html',
  styleUrls: ['./header.scss'],
})
export class HeaderComponent {
  @Output() toggleSidebar = new EventEmitter<void>();
  @Output() sidebarOpened = new EventEmitter<boolean>();

  showUserGroup = false;
  isSidebarOpen = false;

  userName: string = 'User Name';
  profileImage: string = 'assets/images/admin.png';

  constructor(private router: Router, private JobService: JobService, private loader: LoaderService, private ngZone: NgZone) {}
randomToken: string | null = null;
  ngOnInit(): void {
    this.loadUserDetails();
    this.randomToken = localStorage.getItem('randomToken');
    this.router.events.subscribe(event => {
      if (event instanceof NavigationEnd) {
        this.loadUserDetails();
      }
    });

    // Listen for profile update events
    window.addEventListener('profile-updated', () => {
      this.ngZone.run(() => this.loadUserDetails());
    });
  }

  loadUserDetails(): void {
    const userDetails = localStorage.getItem('userDetails');
    if (userDetails) {
      const userObj = JSON.parse(userDetails);
      this.userName = userObj.userName || 'User Name';

      // Use full URL from backend if available, otherwise default
      this.profileImage = userObj.profileImage
        ? userObj.profileImage.startsWith('http')
          ? userObj.profileImage
          : `${this.JobService.fileBaseUrl}/ProfileImages/${userObj.profileImage.split('/').pop()}`
        : 'assets/images/admin.png';
    }
  }

  onToggleSidebar(): void {
    this.isSidebarOpen = !this.isSidebarOpen;
    this.toggleSidebar.emit();
    this.sidebarOpened.emit(this.isSidebarOpen);
  }

  toggleUserGroup(): void {
    this.showUserGroup = !this.showUserGroup;
  }

  logout(): void {
    localStorage.removeItem('userDetails');
    localStorage.removeItem('ss_id');
    localStorage.removeItem('randomToken');
    this.router.navigate(['/login']);
  }
}
