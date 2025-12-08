import { Component, OnInit } from '@angular/core';
import { JobService } from '../../services/job.service';
import Swal from 'sweetalert2';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-role-master',
  templateUrl: './rolemaster.html',
  styleUrls: ['./rolemaster.scss'],
  standalone: true,
  imports: [CommonModule, FormsModule, HeaderComponent, SidenavComponent]
})
export class RoleMasterComponent implements OnInit {
  roles: any[] = [];
  pages: any[] = [];
  permissions: any[] = [];

  newRoleName = '';
  newRoleDesc = '';
  showAddRoleInput = false;

  selectedRole: any = null;
  rolePermissions: { [pageID: number]: boolean } = {};
  showPermissionDialog = false;

  isSidebarHidden = false;

  constructor(private jobService: JobService,private loader:LoaderService) {}

  ngOnInit(): void {
    this.getRoles();
    this.getPages();
    this.getPermissions();
  }

  toggleSidebar() {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  getRoles() {
    this.loader.show();
    this.jobService.getAllRole()
      .pipe(finalize(() => this.loader.hide())) // ✅ always hide loader
      .subscribe({
        next: (res) => {
          this.roles = res || [];
        },
        error: (err) => {
          console.error("Failed to fetch roles:", err);
          this.roles = [];
        }
      });
  }


  getPages() {
    this.loader.show();

    this.jobService.getAllPages()
      .pipe(finalize(() => this.loader.hide())) // ✅ always hides loader
      .subscribe({
        next: (res) => {
          this.pages = res || [];
        },
        error: (err) => {
          console.error("Failed to fetch pages:", err);
          this.pages = [];
        }
      });
  }

  getPermissions() {
    this.loader.show();

    this.jobService.getPagePermissionsByRole(0)
      .pipe(finalize(() => this.loader.hide())) 
      .subscribe({
        next: (res) => {
          this.permissions = res || [];
        },
        error: (err) => {
          console.error("Failed to fetch permissions:", err);
          this.permissions = [];
        }
      });
  }




  addRole() {
    const trimmedName = this.newRoleName.trim().toLowerCase();
    const exists = this.roles.some(
      (r) => r.roleName.trim().toLowerCase() === trimmedName
    );

    if (exists) {
      Swal.fire('Role already exists!', '', 'error');
      return;
    }

    const newRole = {
      roleName: this.newRoleName,
      description: this.newRoleDesc
    };

    console.log('🟢 Add Role Payload:', newRole);
    Swal.fire('Feature coming soon!', 'Add API will be integrated later.', 'info');

    this.newRoleName = '';
    this.newRoleDesc = '';
    this.showAddRoleInput = false;
  }

 deleteRole(role: any, event: Event) {
  event.stopPropagation(); // Prevent tab click
  console.log('Deleting role with entryNo:', role.entryNo);

  Swal.fire({
    title: `Delete role "${role.roleName}"?`,
    text: "This action cannot be undone.",
    icon: 'warning',
    showCancelButton: true,
    confirmButtonText: 'Yes, delete it!',
    cancelButtonText: 'Cancel'
  }).then((result) => {
    if (result.isConfirmed) {
      const entryNo = role.entryNo;
      
      this.loader.show();
      this.jobService.deleteRoleMaster(entryNo)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          this.roles = this.roles.filter(r => r.entryNo !== entryNo);
          if (this.selectedRole?.entryNo === entryNo) {
            this.selectedRole = null;
          }
          Swal.fire('Deleted!', 'Role has been deleted.', 'success');
        },
        error: (err) => {
          console.error('Role deletion failed:', err);
          Swal.fire('Error!', 'Could not delete the role.', 'error');
        }
      });
    }
  });
}



  openPermissionDialog(role: any) {
  this.selectedRole = role;
    this.loader.show();

    this.jobService.getPagePermissionsByRole(role.roleID)
      .pipe(finalize(() => this.loader.hide())) // ✅ always hide loader
      .subscribe({
        next: (res) => {
          this.permissions = res || [];
          this.setRolePermissions();
          this.showPermissionDialog = true;
        },
        error: (err) => {
          console.error("Failed to fetch role permissions:", err);
          this.permissions = [];
          this.showPermissionDialog = false; // keep dialog closed on failure
        }
      });
  }


  setRolePermissions() {
    this.rolePermissions = {};
    const perms = this.permissions.filter((p) => p.roleID === this.selectedRole.roleID);
    for (let page of this.pages) {
      this.rolePermissions[page.pageID] = perms.some((p) => p.pageID === page.pageID);
    }
  }

  togglePermission(pageID: number) {
    this.rolePermissions[pageID] = !this.rolePermissions[pageID];
  }
  onRoleClick(role: any) {
    this.openPermissionDialog(role);
  }

  savePermissions() {
    if (!this.selectedRole) return;

    const selectedPageIDs = Object.entries(this.rolePermissions)
      .filter(([_, checked]) => checked)
      .map(([pageID]) => parseInt(pageID));

    const existing = this.permissions.filter(
      (p) => p.roleID === this.selectedRole.roleID
    );

    const toAdd = selectedPageIDs.filter(
      (id) => !existing.some((p) => p.pageID === id)
    );

    const toRemove = existing.filter((p) => !selectedPageIDs.includes(p.pageID));

    
    toRemove.forEach((perm) => {
      if (perm.entryNo) {
        this.loader.show();
        this.jobService.deleteRolePageMapping(perm.entryNo)
        .pipe(finalize(() => this.loader.hide()))
        .subscribe({
          next: () => console.log(`Deleted mapping for entryNo ${perm.entryNo}`),
          error: (err) => console.error('Failed to delete permission:', err)
        });
      }
    });

    
    toAdd.forEach((pageID) => {
      const mapping = {
        roleID: this.selectedRole.roleID,
        pageID: pageID
      };

      this.loader.show();
      this.jobService.CreateRolePageMapping(mapping)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => console.log(`Added mapping for pageID ${pageID}, roleID: ${this.selectedRole.roleID}`),
        error: (err) => console.error('Failed to add permission:', err)
      });
    });



    Swal.fire('Saved!', 'Permission changes applied successfully.', 'success');
    this.showPermissionDialog = false;
  }


}
