import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PostedJobTransaction } from './posted-job-transaction';

describe('PostedJobTransaction', () => {
  let component: PostedJobTransaction;
  let fixture: ComponentFixture<PostedJobTransaction>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PostedJobTransaction]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PostedJobTransaction);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
