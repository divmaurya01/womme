import { ComponentFixture, TestBed } from '@angular/core/testing';

import { JobListComponent_copy } from './job-list-copy';

describe('JobListComponent_copy', () => {
  let component: JobListComponent_copy;
  let fixture: ComponentFixture<JobListComponent_copy>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JobListComponent_copy]
    })
    .compileComponents();

    fixture = TestBed.createComponent(JobListComponent_copy);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
