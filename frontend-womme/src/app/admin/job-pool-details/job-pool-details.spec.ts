import { ComponentFixture, TestBed } from '@angular/core/testing';

import { JobPoolDetails } from './job-pool-details';

describe('UnpostedJobTransaction', () => {
  let component: JobPoolDetails;
  let fixture: ComponentFixture<JobPoolDetails>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JobPoolDetails]
    })
    .compileComponents();

    fixture = TestBed.createComponent(JobPoolDetails);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
