import { Component, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
 
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
import Swal from 'sweetalert2';
 
@Component({
  selector: 'app-edit-profile',
  templateUrl: './edit-profile.html',
  styleUrls: ['./edit-profile.scss'],
  standalone: true,
  imports: [HeaderComponent, SidenavComponent, CommonModule, FormsModule],
})
export class EditProfileComponent implements OnInit {

  employeeCode = '';
  emp_num = 0;
  userName = '';
  roleID = 0;
  passwordHash = '';
  isActive = false;
  womm_id: number | string = '';
  roleName: string = '';
formError = '';
  rolesList: { roleID: number; roleName: string }[] = [];

  showPassword = false;
  editDetailsMode = false;
  editImageMode = false;

  profileImage = 'assets/images/admin.png';
  previewImage: string | ArrayBuffer | null = null;
  selectedFile: File | null = null;

   isSidebarHidden = window.innerWidth <= 1024;
  loggedInUser: any;

  constructor(private jobService: JobService, private loader: LoaderService) {}

  ngOnInit(): void {
    this.checkScreenSize();
    const userData = localStorage.getItem('userDetails');

    if (userData) {
      this.loggedInUser = JSON.parse(userData);
      this.employeeCode = this.loggedInUser.employeeCode;

      // 1️⃣ Load roles first
      this.loadRoles();
    } else {
      console.warn('No userDetails found in localStorage.');
    }
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
  // ✅ Load all roles
  loadRoles(): void {
    this.jobService.getAllRole().subscribe({
      next: (roles: any[]) => {
        this.rolesList = roles.map(r => ({
          roleID: r.roleID,
          roleName: r.roleName
        }));

        // 2️⃣ After roles loaded → fetch user
        this.fetchUserFromDb(this.employeeCode);
      },
      error: (err) => {
        console.error("Failed to fetch roles:", err);
      }
    });
  }

  // ✅ Fetch user
  fetchUserFromDb(empCode: string): void {
    this.loader.show();

    this.jobService.UserMaster()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (users: any[]) => {

          const matchedUser = users.find(u => u.emp_num === empCode);

          if (matchedUser) {

            this.emp_num = matchedUser.emp_num;
            this.userName = matchedUser.name;
            this.roleID = matchedUser.roleID;
            this.passwordHash = matchedUser.passwordHash;
            this.isActive = matchedUser.isActive;

            this.womm_id = 'WME' +
              matchedUser.womm_id.toString().padStart(5, '0');

            // ✅ Set role name from rolesList
            const role = this.rolesList.find(r => r.roleID === this.roleID);
            this.roleName = role ? role.roleName : 'Unknown';

            // Profile Image
            if (matchedUser.profileImage?.trim()) {
              this.profileImage =
                `${this.jobService.fileBaseUrl}/ProfileImages/${matchedUser.profileImage.split('/').pop()}`;
            } else {
              this.profileImage = '../../../assets/images/favicon-96x96.png';
            }
          } else {
            console.warn(`No user found with employeeCode: ${empCode}`);
            this.profileImage = '../../../assets/images/favicon-96x96.png';
          }
        },
        error: (err) => {
          console.error("Failed to fetch users from DB:", err);
          this.profileImage = '../../../assets/images/favicon-96x96.png';
        }
      });
  }

  // ✅ Role dropdown change
  onRoleChange(selectedRoleID: number): void {
    this.roleID = +selectedRoleID;

    const role = this.rolesList.find(r => r.roleID === this.roleID);
    this.roleName = role ? role.roleName : '';
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  toggleEditImage(): void {
    this.editImageMode = !this.editImageMode;
  }

  toggleEditDetails(): void {
    this.editDetailsMode = !this.editDetailsMode;
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

 onFileSelected(event: Event): void {
  const input = event.target as HTMLInputElement;

  if (input.files && input.files[0]) {
    this.selectedFile = input.files[0];

    const reader = new FileReader();
    reader.onload = () => {
      this.previewImage = reader.result;
      this.profileImage = this.previewImage as string; // ✅ show preview immediately
    };
    reader.readAsDataURL(this.selectedFile);
  }
}


  saveProfileImage(): void {

    if (!this.selectedFile) {
      alert("Please select an image first.");
      return;
    }

    const formData = new FormData();
    formData.append('emp_num', this.employeeCode);
    formData.append('profileImage', this.selectedFile);

    this.loader.show();

    this.jobService.updateUserProfileImages(formData)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any) => {
          this.profileImage = res.imageUrl;
          this.previewImage = null;
          this.selectedFile = null;
          this.editImageMode = false;

          const storedUser = JSON.parse(localStorage.getItem('userDetails')!);
          storedUser.profileImage = res.imageUrl;
          localStorage.setItem('userDetails', JSON.stringify(storedUser));

          window.dispatchEvent(new Event('profile-updated'));
        },
        error: (err) => {
          console.error("Failed to update image:", err);
          alert("Failed to update profile image.");
        }
      });
  }
  

  saveProfileDetails() {
  // Basic validation (example: username required)
  if (!this.userName || !this.roleID) {
    this.formError = 'Please fill all required fields.';
    return;
  }

  const payload = {
    emp_num: this.emp_num,
    name: this.userName.trim(),
    roleID: this.roleID,
    passwordHash: this.passwordHash,  // keep existing password
    isActive: this.isActive,
    createdBy: this.loggedInUser?.employeeCode || 'system admin',
    profileImage: this.profileImage
  };

  this.loader.show();

  // Call update API
  this.jobService.updateEmployee(this.emp_num.toString(), payload)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: () => {
        Swal.fire('Updated', 'Profile updated successfully.', 'success');
        this.editDetailsMode = false;
        this.fetchUserFromDb(this.employeeCode); // Refresh displayed data
      },
      error: (err) => {
        this.formError = err.error?.message || 'Error updating profile.';
      }
    });
}

  
}
