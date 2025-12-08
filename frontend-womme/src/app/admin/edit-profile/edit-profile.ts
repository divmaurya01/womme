import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
 
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
 
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
 
  showPassword = false;
  editDetailsMode = false;
  editImageMode = false;
 
  profileImage = 'assets/images/admin.png';
  previewImage: string | ArrayBuffer | null = null;
  selectedFile: File | null = null;
 
  isSidebarHidden = false;
  loggedInUser: any;
 
  constructor(private jobService: JobService,private loader:LoaderService) {}
 
  ngOnInit(): void {
    const userData = localStorage.getItem('userDetails');
    if (userData) {
      this.loggedInUser = JSON.parse(userData);
      this.employeeCode = this.loggedInUser.employeeCode;
      this.fetchUserFromDb(this.employeeCode);
    } else {
      console.warn('No userDetails found in localStorage.');
    }
  }
 
  fetchUserFromDb(empCode: string): void {
  console.log("Fetching user for:", empCode);

  this.loader.show();
  this.jobService.UserMaster()
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (users: any[]) => {
        console.log("Users from DB:", users);

        const matchedUser = users.find(u => u.employeeCode === empCode);

        if (matchedUser) {
          this.emp_num = matchedUser.emp_num;
          this.userName = matchedUser.userName;
          this.roleID = matchedUser.roleID;
          this.passwordHash = matchedUser.passwordHash;
          this.isActive = matchedUser.isActive;

          // ✅ Ensure fallback to default image
          if (matchedUser.profileImage && matchedUser.profileImage.trim() !== "") {
            this.profileImage = `${this.jobService.fileBaseUrl}/ProfileImages/${matchedUser.profileImage.split('/').pop()}`;
          } else {
            this.profileImage = '../../../assets/images/favicon-96x96.png';
          }

          console.log("Matched User:", matchedUser);
          console.log("Resolved profileImage:", this.profileImage);
        } else {
          console.warn(`No user found with employeeCode: ${empCode}`);
          this.profileImage = '../../../assets/images/favicon-96x96.png'; // fallback if no user
        }
      },
      error: (err) => {
        console.error("Failed to fetch users from DB:", err);
        this.profileImage = '../../../assets/images/favicon-96x96.png'; // fallback if error
      }
    });
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

  
}