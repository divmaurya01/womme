import { ComponentFixture, TestBed } from '@angular/core/testing';

import { UnpostedJobTransaction } from './unposted-job-transaction';

describe('UnpostedJobTransaction', () => {
  let component: UnpostedJobTransaction;
  let fixture: ComponentFixture<UnpostedJobTransaction>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UnpostedJobTransaction]
    })
    .compileComponents();

    fixture = TestBed.createComponent(UnpostedJobTransaction);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
