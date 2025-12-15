using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;



namespace WommeAPI.Models
{


    public class AssignedJob
    {
        [Key]
        public int EntryNo { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string JobNumber { get; set; } = string.Empty;
        public decimal? AssignedHours { get; set; }
        public string? Remark { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class StartJobRequestDto
    {
        public string JobNumber { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
        public string Wc { get; set; } = string.Empty;
        public int QtyReleased { get; set; }
        public int OperationNumber { get; set; }
        public string MachineNumber { get; set; } = string.Empty;
        public string EmpNum { get; set; } = string.Empty;
        public string loginuser { get; set; } = string.Empty;
        public string? Item { get; set; }
    }


// public class UpdateJobLogDto{
//     public decimal TransNum { get; set; }         // <- importantpublic string Job { get; set; }
//     public string SerialNumber { get; set; }
//     public int OperationNumber { get; set; }
//     public string WorkCenter { get; set; }
//     public string EmpNum { get; set; }
//     public string Shift { get; set; }
//     public DateTime? StartTime { get; set; }
//     public DateTime? EndTime { get; set; }
//     public decimal? JobRate { get; set; }
//     public string Status { get; set; }
//     public string UpdatedBy { get; set; }

//     // Extra fields that are good to include for Sytelinepublic string Item { get; set; }
//     public string TransType { get; set; }         // 'O' or 'R' when neededpublic int? Suffix { get; set; }
//     public int? NextOper { get; set; }
//     public int? Posted { get; set; }
//     public string TransClass { get; set; }
//     public decimal? QtyComplete { get; set; }
//     public decimal? QtyScrapped { get; set; }
//     public Guid? RowPointer { get; set; }
// }



    public class EmpMechCheckRequestDto
    {
        public string Job { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string TransNum { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
    }

    // DTO for response
public class UpdateJobLogDto
{
    public decimal TransNum { get; set; }     // must be present and passed to Syteline
    public string Job { get; set; }
    public string SerialNumber { get; set; }
    public int OperationNumber { get; set; }
    public string WorkCenter { get; set; }
    public string EmpNum { get; set; }
    public string Shift { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal? JobRate { get; set; }
    public string Status { get; set; }
    public string UpdatedBy { get; set; }
    public string? TransClass { get; set; }     // Required by SytelineService
public int? Posted { get; set; }     
 public string? SiteRef { get; set; }         // Required by Syteline Update SQL
public string? MachineNum { get; set; }

    // optional but helpful
   public string? Item { get; set; }
public string? TransType { get; set; }

    public int? Suffix { get; set; }
    public int? NextOper { get; set; }
    public decimal? QtyComplete { get; set; }
    public decimal? QtyScrapped { get; set; }
    public Guid? RowPointer { get; set; }
}




    public class JobStartRequest
    {
        public string Job { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty; // matches JSON
        public string TransNum { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
    }


    [Table("EmployeeLog")]
    public class EmployeeLog
    {
        [Key]
        public int EntryNo { get; set; }

        [Column("Job")]
        public string? JobNumber { get; set; }

        [Column("emp_num")]
        public string EmployeeCode { get; set; } = string.Empty;

        [Column("machine_num")]
        public string? MachineNumber { get; set; }

        [Column("oper_num")]
        public string? OperationNumber { get; set; }

        [Column("trans_num")]
        public string? TransNumber { get; set; }

        [Column("wc")]
        public string? WorkCenter { get; set; }

        [Column("status_id")]
        public int StatusID { get; set; }

        [Column("status_time")]
        public DateTime StatusTime { get; set; }

        [Column("serialNo")]
        public string? serialNo { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }


    public class ItemMaster
    {
        [Key]
        public int EntryNo { get; set; }
        public int ItemNumber { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? ItemDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class MachineMaster
    {
        [Key]
        public int EntryNo { get; set; }
        public string? MachineNumber { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string? MachineDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class OperationMaster
    {
        [Key]
        public int EntryNo { get; set; }
        public int OperationNumber { get; set; }
        public string OperationName { get; set; } = string.Empty;
        public string? OperationDescription { get; set; }
        public string? wc { get; set; }               // New
        public string? wc_description { get; set; }   // New
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PageMaster
    {
        [Key]
        public int EntryNo { get; set; }
        public int PageID { get; set; }
        public string PageName { get; set; } = string.Empty;
        public string? PageURL { get; set; }
        public string? ParentPageID { get; set; }
        public int? DisplayOrder { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class RoleMaster
    {
        [Key]
        public int EntryNo { get; set; }
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class RolePageMapping
    {
        [Key]
        public int EntryNo { get; set; }
        public int RoleID { get; set; }
        public int PageID { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class StatusMaster
    {
        [Key]
        public int StatusID { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UserMaster
    {
        [Key]
        public int EntryNo { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int RoleID { get; set; }
        public bool IsActive { get; set; }
        public string? ProfileImage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UserToken
    {
        [Key]
        public int EntryNo { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string TokenNumber { get; set; } = string.Empty;
        public DateTime ValidTill { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Job
    {
        [Key]
        public int EntryNo { get; set; }
        public string JobNumber { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public string? JobDescription { get; set; }
        public int JobQuantity { get; set; }
        public int? OutputQuantity { get; set; }
        public string ItemNumber { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string? ItemDescription { get; set; }
        public string MachineNumber { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string? MachineDescription { get; set; }
        public string OperationNumber { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
        public string? OperationDescription { get; set; }
        public decimal? EstimatedHours { get; set; }
        public string? Shift { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // public class JobStatusResponseDto
    // { 
    //     public string? JobNumber { get; set; }      
    //     public string? EmployeeCode { get; set; }
    //     public string? UserName { get; set; }
    //     public int StatusID { get; set; }
    //     public string? StatusName { get; set; }
    // }

    public class PrintLog
    {
        [Key]
        public int EntryNo { get; set; }

        [Required]
        public string? EmployeeCode { get; set; }

        [Required]
        public string? Logger { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class UpdateJobStatusDto
    {
        public int EntryNo { get; set; }
        public string? JobNumber { get; set; }
        public string JobName { get; set; } = string.Empty;
        public string JobDescription { get; set; } = string.Empty;
        public int JobQuantity { get; set; }
        public int OutputQuantity { get; set; }
        public string ItemNumber { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public string MachineNumber { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string MachineDescription { get; set; } = string.Empty;
        public string OperationNumber { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
        public string OperationDescription { get; set; } = string.Empty;
        public decimal EstimatedHours { get; set; }
        public string Shift { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Optional AssignedJob data
        public decimal? AssignedHours { get; set; }
        public string? Remark { get; set; }

        // Optional Log data
        public string? EmployeeCode { get; set; }
        public int? StatusID { get; set; }
    }

    public class AssignedJobQrDto
    {
        public int EntryNo { get; set; }
        public string? EmployeeCode { get; set; }
        public string? JobNumber { get; set; }
        public int AssignedHours { get; set; }
        public string? Remark { get; set; }
    }


    public class QrValidationRequestDto
    {
        public string? EmployeeCode { get; set; }
        public string? JobNumber { get; set; }
    }

    public class UpdateUserImageRequest
    {
        public int EntryNo { get; set; }
        public string? ProfileImage { get; set; } // can be URL or base64 string
    }

    public class JobMaterial
    {
        [Key]
        public int EntryNo { get; set; }

        public string? JobNumber { get; set; }

        public int OperationNo { get; set; }

        public int WorkCenter { get; set; }

        public string? WorkCenterDescription { get; set; }

        public string? MaterialName { get; set; }

        public string? MaterialDescription { get; set; }

        public int? ItemNumber { get; set; }

        public string? ItemDescription { get; set; }

        public int OutputQuantity { get; set; }
    }

    public class JobStatusDto
    {
        public string? JobNumber { get; set; }
        public string? JobName { get; set; }
        public string? UserName { get; set; }
        public string? StatusName { get; set; }
        public double EstimatedHours { get; set; }
        public double? ConsumedHours { get; set; }
        public double? OvertimeHours { get; set; }
    }


    [Table("job_mst")]
    public class JobMst
    {
        [Key, Column("job")]
        public string? job { get; set; } = string.Empty;

        [Column("suffix")]
        public short suffix { get; set; }

        [Column("site_ref")]
        public string? site_ref { get; set; }

        [Column("type")]
        public string? type { get; set; }

        [Column("job_date")]
        public DateTime? job_date { get; set; }

        [Column("cust_num")]
        public string? cust_num { get; set; }

        [Column("ord_type")]
        public string? ord_type { get; set; }

        [Column("ord_num")]
        public string? ord_num { get; set; }

        [Column("ord_line")]
        public short? ord_line { get; set; }

        [Column("ord_release")]
        public short? ord_release { get; set; }

        [Column("est_job")]
        public string? est_job { get; set; }

        [Column("est_suf")]
        public short? est_suf { get; set; }

        [Column("item")]
        public string? item { get; set; }

        [Column("qty_released")]
        public decimal qty_released { get; set; }

        [Column("qty_complete")]
        public decimal? qty_complete { get; set; }

        [Column("qty_scrapped")]
        public decimal? qty_scrapped { get; set; }

        [Column("stat")]
        public string? stat { get; set; }

        [Column("lst_trx_date")]
        public DateTime? lst_trx_date { get; set; }

        [Column("root_job")]
        public string? root_job { get; set; }

        [Column("root_suf")]
        public short? root_suf { get; set; }

        [Column("ref_job")]
        public string? ref_job { get; set; }

        [Column("ref_suf")]
        public short ref_suf { get; set; }

        [Column("ref_oper")]
        public int ref_oper { get; set; }

        [Column("ref_seq")]
        public short? ref_seq { get; set; }

        [Column("low_level")]
        public byte? low_level { get; set; }

        [Column("effect_date")]
        public DateTime? effect_date { get; set; }

        [Column("wip_acct")]
        public string? wip_acct { get; set; }

        [Column("wip_total")]
        public decimal? wip_total { get; set; }

        [Column("wip_complete")]
        public decimal? wip_complete { get; set; }

        [Column("wip_special")]
        public decimal? wip_special { get; set; }

        [Column("revision")]
        public string? revision { get; set; }

        [Column("picked")]
        public byte? picked { get; set; }

        [Column("whse")]
        public string? whse { get; set; }

        [Column("jcb_acct")]
        public string? jcb_acct { get; set; }

        [Column("ps_num")]
        public string? ps_num { get; set; }

        [Column("wip_lbr_acct")]
        public string? wip_lbr_acct { get; set; }

        [Column("wip_fovhd_acct")]
        public string? wip_fovhd_acct { get; set; }

        [Column("wip_vovhd_acct")]
        public string? wip_vovhd_acct { get; set; }

        [Column("wip_out_acct")]
        public string? wip_out_acct { get; set; }

        [Column("description")]
        public string? description { get; set; }

        [Column("recorddate")]
        public DateTime RecordDate { get; set; }

        [Column("rowpointer")]
        public Guid RowPointer { get; set; }

        [Column("createdby")]
        public string? CreatedBy { get; set; }

        [Column("updatedby")]
        public string? UpdatedBy { get; set; }

        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [Column("noteexistsflag")]
        public byte NoteExistsFlag { get; set; }

        [Column("inworkflow")]
        public byte InWorkflow { get; set; }

        [Column("scheduled")]
        public byte Scheduled { get; set; }

        [Column("export_type")]
        public string? ExportType { get; set; }

        [Column("contains_tax_free_matl")]
        public byte ContainsTaxFreeMatl { get; set; }

        [Column("rework")]
        public byte Rework { get; set; }

        [Column("unlinked_xref")]
        public byte UnlinkedXref { get; set; }

        [Column("is_external")]
        public byte IsExternal { get; set; }

        [Column("preassign_lots")]
        public byte PreassignLots { get; set; }

        [Column("preassign_serials")]
        public byte PreassignSerials { get; set; }

        [Column("MO_co_job")]
        public byte MOCoJob { get; set; }

        [Column("Uf_JobMatlClass")]
        [StringLength(80)]
        public string? Uf_JobMatlClass { get; set; }

        [Column("Uf_MatlDescJob")]
        public string? Uf_MatlDescJob { get; set; }

        [Column("Uf_JobTempClass")]
        [StringLength(80)]
        public string? Uf_JobTempClass { get; set; }

        [Column("Uf_RouterRevDate")]
        public DateTime? Uf_RouterRevDate { get; set; }

        [Column("Uf_RouterRevNo")]
        public int? Uf_RouterRevNo { get; set; }

        [Column("Uf_RouterRevRemarks")]
        [StringLength(2000)]
        public string? Uf_RouterRevRemarks { get; set; }

        [Column("Uf_JobPreparedBy")]
        [StringLength(128)]
        public string? Uf_JobPreparedBy { get; set; }

        [Column("Uf_DrawingNo")]
        [StringLength(25)]
        public string? Uf_DrawingNo { get; set; }

        [Column("Uf_JobPSL")]
        public string? Uf_JobPsl { get; set; }

        //  [Column("Uf_DrawingRevision")]
        //  public int? Uf_DrawingRevision { get; set; }

        [Column("Uf_Active")]
        public byte? Uf_Active { get; set; }


    }


    [Table("jobroute_mst")]
    public class JobRouteMst
    {
        [Key]
        [Column("job")]
        public string? Job { get; set; }

        [Column("suffix")]
        public short Suffix { get; set; }

        [Column("oper_num")]
        public int OperNum { get; set; }

        [Column("site_ref")]
        public string? SiteRef { get; set; }

        [Column("wc")]
        public string? Wc { get; set; }

        [Column("setup_hrs_t")]
        public decimal? SetupHrsT { get; set; }

        [Column("setup_cost_t")]
        public decimal? SetupCostT { get; set; }

        [Column("complete")]
        public byte? Complete { get; set; }

        [Column("setup_hrs_v")]
        public decimal? SetupHrsV { get; set; }

        [Column("wip_amt")]
        public decimal? WipAmt { get; set; }

        [Column("qty_scrapped")]
        public decimal? QtyScrapped { get; set; }

        [Column("qty_received")]
        public decimal? QtyReceived { get; set; }

        [Column("qty_moved")]
        public decimal? QtyMoved { get; set; }

        [Column("qty_complete")]
        public decimal? QtyComplete { get; set; }

        [Column("effect_date")]
        public DateTime? EffectDate { get; set; }

        [Column("obs_date")]
        public DateTime? ObsDate { get; set; }

        [Column("bflush_type")]
        public string? BflushType { get; set; }

        [Column("run_basis_lbr")]
        public string? RunBasisLbr { get; set; }

        [Column("run_basis_mch")]
        public string? RunBasisMch { get; set; }

        [Column("fixovhd_t_lbr")]
        public decimal? FixovhdTLbr { get; set; }

        [Column("fixovhd_t_mch")]
        public decimal? FixovhdTMch { get; set; }

        [Column("varovhd_t_lbr")]
        public decimal? VarovhdTLbr { get; set; }

        [Column("varovhd_t_mch")]
        public decimal? VarovhdTMch { get; set; }

        [Column("run_hrs_t_lbr")]
        public decimal? RunHrsTLbr { get; set; }

        [Column("run_hrs_t_mch")]
        public decimal? RunHrsTMch { get; set; }

        [Column("run_hrs_v_lbr")]
        public decimal? RunHrsVLbr { get; set; }

        [Column("run_hrs_v_mch")]
        public decimal? RunHrsVMch { get; set; }

        [Column("run_cost_t_lbr")]
        public decimal? RunCostTLbr { get; set; }

        [Column("cntrl_point")]
        public byte? CntrlPoint { get; set; }

        [Column("setup_rate")]
        public decimal SetupRate { get; set; }

        [Column("efficiency")]
        public decimal? Efficiency { get; set; }

        [Column("fovhd_rate_mch")]
        public decimal? FovhdRateMch { get; set; }

        [Column("vovhd_rate_mch")]
        public decimal? VovhdRateMch { get; set; }

        [Column("run_rate_lbr")]
        public decimal RunRateLbr { get; set; }

        [Column("varovhd_rate")]
        public decimal? VarovhdRate { get; set; }

        [Column("fixovhd_rate")]
        public decimal? FixovhdRate { get; set; }

        [Column("wip_matl_amt")]
        public decimal? WipMatlAmt { get; set; }

        [Column("wip_lbr_amt")]
        public decimal? WipLbrAmt { get; set; }

        [Column("wip_fovhd_amt")]
        public decimal? WipFovhdAmt { get; set; }

        [Column("wip_vovhd_amt")]
        public decimal? WipVovhdAmt { get; set; }

        [Column("wip_out_amt")]
        public decimal? WipOutAmt { get; set; }

        [Column("NoteExistsFlag")]
        public byte NoteExistsFlag { get; set; }

        [Column("RecordDate")]
        public DateTime RecordDate { get; set; }

        [Column("RowPointer")]
        public Guid RowPointer { get; set; }

        [Column("CreatedBy")]
        public string? CreatedBy { get; set; }

        [Column("UpdatedBy")]
        public string? UpdatedBy { get; set; }

        [Column("CreateDate")]
        public DateTime CreateDate { get; set; }

        [Column("InWorkflow")]
        public byte InWorkflow { get; set; }

        [Column("yield")]
        public decimal Yield { get; set; }

        [Column("opm_consec_oper")]
        public byte OpmConsecOper { get; set; }

        [Column("MO_shared")]
        public byte MOShared { get; set; }

        [Column("MO_seconds_per_cycle")]
        public decimal MOSecondsPerCycle { get; set; }

        [Column("MO_formula_matl_weight")]
        public decimal? MOFormulaMatlWeight { get; set; }

        [Column("MO_formula_matl_weight_units")]
        public string? MOFormulaMatlWeightUnits { get; set; }

        [Column("Uf_BOPOper")]
        public byte? UfBOPOper { get; set; }

        [Column("Uf_Operation")]
        public string? UfOperation { get; set; }

        [Column("Uf_outside")]
        public byte? UfOutside { get; set; }

        [Column("Uf_rework")]
        public byte? UfRework { get; set; }

        [Column("Uf_RouteRemarks")]
        public string? UfRouteRemarks { get; set; }

        [Column("Uf_Wip")]
        public byte? UfWip { get; set; }

        [Column("Uf_TaskDescription")]
        public string? UfTaskDescription { get; set; }

        //  public int? MachineNumber { get; set; } 

    }


    [Table("jobmatl_mst")]
    public class JobmatlMst
    {
        [Key]
        [Column("job")]
        public string? Job { get; set; }

        [Column("suffix")]
        public short Suffix { get; set; }

        [Column("oper_num")]
        public int OperNum { get; set; }

        [Column("sequence")]
        public short Sequence { get; set; }

        [Column("site_ref")]
        public string? SiteRef { get; set; }

        [Column("matl_type")]
        public string? MatlType { get; set; }

        [Column("item")]
        public string? Item { get; set; }

        [Column("matl_qty")]
        public decimal? MatlQty { get; set; }

        [Column("units")]
        public string? Units { get; set; }

        [Column("cost")]
        public decimal Cost { get; set; }

        [Column("qty_issued")]
        public decimal? QtyIssued { get; set; }

        [Column("a_cost")]
        public decimal? ACost { get; set; }

        [Column("ref_type")]
        public string? RefType { get; set; }

        [Column("ref_num")]
        public string? RefNum { get; set; }

        [Column("ref_line_suf")]
        public short? RefLineSuf { get; set; }

        [Column("ref_release")]
        public short? RefRelease { get; set; }

        [Column("po_unit_cost")]
        public decimal? PoUnitCost { get; set; }

        [Column("effect_date")]
        public DateTime? EffectDate { get; set; }

        [Column("obs_date")]
        public DateTime? ObsDate { get; set; }

        [Column("scrap_fact")]
        public decimal? ScrapFact { get; set; }

        [Column("qty_var")]
        public decimal? QtyVar { get; set; }

        [Column("fixovhd_t")]
        public decimal? FixovhdT { get; set; }

        [Column("varovhd_t")]
        public decimal? VarovhdT { get; set; }

        [Column("feature")]
        public string? Feature { get; set; }

        [Column("probable")]
        public decimal? Probable { get; set; }

        [Column("opt_code")]
        public string? OptCode { get; set; }

        [Column("inc_price")]
        public decimal? IncPrice { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("pick_date")]
        public DateTime? PickDate { get; set; }

        [Column("bom_seq")]
        public short? BomSeq { get; set; }

        [Column("matl_qty_conv")]
        public decimal? MatlQtyConv { get; set; }

        [Column("u_m")]
        public string? UM { get; set; }

        [Column("inc_price_conv")]
        public decimal? IncPriceConv { get; set; }

        [Column("cost_conv")]
        public decimal? CostConv { get; set; }

        [Column("backflush")]
        public byte? Backflush { get; set; }

        [Column("bflush_loc")]
        public string? BflushLoc { get; set; }

        [Column("fmatlovhd")]
        public decimal? Fmatlovhd { get; set; }

        [Column("vmatlovhd")]
        public decimal? Vmatlovhd { get; set; }

        [Column("matl_cost")]
        public decimal MatlCost { get; set; }

        [Column("lbr_cost")]
        public decimal LbrCost { get; set; }

        [Column("fovhd_cost")]
        public decimal FovhdCost { get; set; }

        [Column("vovhd_cost")]
        public decimal VovhdCost { get; set; }

        [Column("out_cost")]
        public decimal OutCost { get; set; }

        [Column("a_matl_cost")]
        public decimal? AMatlCost { get; set; }

        [Column("a_lbr_cost")]
        public decimal? ALbrCost { get; set; }

        [Column("a_fovhd_cost")]
        public decimal? AFovhdCost { get; set; }

        [Column("a_vovhd_cost")]
        public decimal? AVovhdCost { get; set; }

        [Column("a_out_cost")]
        public decimal? AOutCost { get; set; }

        [Column("matl_cost_conv")]
        public decimal? MatlCostConv { get; set; }

        [Column("lbr_cost_conv")]
        public decimal? LbrCostConv { get; set; }

        [Column("fovhd_cost_conv")]
        public decimal? FovhdCostConv { get; set; }

        [Column("vovhd_cost_conv")]
        public decimal? VovhdCostConv { get; set; }

        [Column("out_cost_conv")]
        public decimal? OutCostConv { get; set; }

        [Column("NoteExistsFlag")]
        public byte NoteExistsFlag { get; set; }

        [Column("RecordDate")]
        public DateTime RecordDate { get; set; }

        [Column("RowPointer")]
        public Guid RowPointer { get; set; }

        [Column("CreatedBy")]
        public string? CreatedBy { get; set; }

        [Column("UpdatedBy")]
        public string? UpdatedBy { get; set; }

        [Column("CreateDate")]
        public DateTime CreateDate { get; set; }

        [Column("InWorkflow")]
        public byte InWorkflow { get; set; }

        [Column("alt_group")]
        public short AltGroup { get; set; }

        [Column("alt_group_rank")]
        public short AltGroupRank { get; set; }

        [Column("planned_alternate")]
        public byte? PlannedAlternate { get; set; }

        [Column("new_sequence")]
        public short? NewSequence { get; set; }

        [Column("manufacturer_id")]
        public string? ManufacturerId { get; set; }

        [Column("manufacturer_item")]
        public string? ManufacturerItem { get; set; }

        [Column("PP_matl_is_paper")]
        public byte PPMatlIsPaper { get; set; }

        [Column("MO_formula_matl_weight_pct")]
        public decimal? MOFormulaMatlWeightPct { get; set; }

        [Column("Uf_action_completed")]
        public string? UfActionCompleted { get; set; }

        [Column("Uf_APISpecTitle")]
        public string? UfAPISpecTitle { get; set; }

        [Column("Uf_BOPItem")]
        public byte? UfBOPItem { get; set; }

        [Column("Uf_jobItem_Source")]
        public string? UfJobItemSource { get; set; }

        [Column("Uf_JobMatlRemarks")]
        public string? UfJobMatlRemarks { get; set; }

        [Column("Uf_last_po_cost")]
        public decimal? UfLastPoCost { get; set; }

        [Column("Uf_last_po_num")]
        public string? UfLastPoNum { get; set; }

        [Column("Uf_last_po_qty_ord")]
        public decimal? UfLastPoQtyOrd { get; set; }

        [Column("Uf_last_vend_name")]
        public string? UfLastVendName { get; set; }

        [Column("Uf_last_vend_num")]
        public string? UfLastVendNum { get; set; }

        [Column("Uf_OrderDate")]
        public DateTime? UfOrderDate { get; set; }

        [Column("Uf_OrderNo")]
        public string? UfOrderNo { get; set; }

        [Column("Uf_PromiseDate")]
        public DateTime? UfPromiseDate { get; set; }

        [Column("Uf_Pull")]
        public byte? UfPull { get; set; }

        [Column("uf_reservedjobmatl")]
        public decimal? UfReservedJobMatl { get; set; }

        [Column("Uf_TaskDescription")]
        public string? UfTaskDescription { get; set; }

        [Column("Uf_ID")]
        public string? UfID { get; set; }

        [Column("Uf_IDRaw")]
        public string? UfIDRaw { get; set; }

        [Column("Uf_Length")]
        public string? UfLength { get; set; }

        [Column("Uf_LengthRaw")]
        public string? UfLengthRaw { get; set; }

        [Column("Uf_MatlSpec")]
        public string? UfMatlSpec { get; set; }

        [Column("Uf_OD")]
        public string? UfOD { get; set; }

        [Column("Uf_ODRaw")]
        public string? UfODRaw { get; set; }

        [Column("Uf_PlateTkns")]
        public string? UfPlateTkns { get; set; }

        [Column("Uf_PlateTknsRaw")]
        public string? UfPlateTknsRaw { get; set; }

        [Column("Uf_RawForm")]
        public string? UfRawForm { get; set; }

        [Column("Uf_RawRevision")]
        public short? UfRawRevision { get; set; }

        [Column("Uf_Width")]
        public string? UfWidth { get; set; }

        [Column("Uf_WidthRaw")]
        public string? UfWidthRaw { get; set; }

        [Column("Uf_CustName")]
        public string? UfCustName { get; set; }

        [Column("Uf_ItemDescription2")]
        public string? UfItemDescription2 { get; set; }

        [Column("Uf_serial")]
        public string? UfSerial { get; set; }

        [Column("Uf_MtlHeatCode")]
        public string? UfMtlHeatCode { get; set; }

        [Column("Uf_MtlSpec")]
        public string? UfMtlSpec { get; set; }

        [Column("Uf_WOMBomSeq")]
        public string? UfWOMBomSeq { get; set; }
    }

    [Table("wc_mst")]
    public class WcMst
    {
        [Column(Order = 0)]
        public string? site_ref { get; set; }

        [Key]
        [Column(Order = 1)]
        public string? wc { get; set; }

        public string? description { get; set; }
        public byte? outside { get; set; }
        public decimal setup_rate { get; set; }
        public decimal? efficiency { get; set; }
        public string? dept { get; set; }
        public string? alternate { get; set; }
        public decimal? queue_hrs_a { get; set; }
        public decimal? queue_qty_t { get; set; }
        public decimal? setup_rate_a { get; set; }
        public decimal? setup_hrs_t { get; set; }
        public string? calendar { get; set; }
        public string? charfld1 { get; set; }
        public string? charfld2 { get; set; }
        public string? charfld3 { get; set; }
        public decimal? decifld1 { get; set; }
        public decimal? decifld2 { get; set; }
        public decimal? decifld3 { get; set; }
        public byte? logifld { get; set; }
        public DateTime? datefld { get; set; }
        public decimal? queue_ticks { get; set; }
        public string? bflush_type { get; set; }
        public decimal? run_hrs_t_mch { get; set; }
        public decimal? fovhd_rate_mch { get; set; }
        public decimal? vovhd_rate_mch { get; set; }
        public string? fmco_acct { get; set; }
        public string? vmco_acct { get; set; }
        public decimal? run_hrs_t_lbr { get; set; }
        public decimal run_rate_lbr { get; set; }
        public decimal? run_rate_a_lbr { get; set; }
        public string? overhead { get; set; }
        public byte cntrl_point { get; set; }
        public string? wip_matl_acct { get; set; }
        public string? wip_lbr_acct { get; set; }
        public string? wip_fovhd_acct { get; set; }
        public string? wip_vovhd_acct { get; set; }
        public string? wip_out_acct { get; set; }
        public string? muv_acct { get; set; }
        public string? lrv_acct { get; set; }
        public string? luv_acct { get; set; }
        public string? fmouv_acct { get; set; }
        public string? vmouv_acct { get; set; }
        public string? flouv_acct { get; set; }
        public string? vlouv_acct { get; set; }
        public string? fmcouv_acct { get; set; }
        public string? vmcouv_acct { get; set; }
        public decimal? wip_matl_total { get; set; }
        public decimal? wip_lbr_total { get; set; }
        public decimal? wip_fovhd_total { get; set; }
        public decimal? wip_vovhd_total { get; set; }
        public decimal? wip_out_total { get; set; }

        // Account Unit Fields (Grouped as logical sets)
        public string? fmco_acct_unit1 { get; set; }
        public string? fmco_acct_unit2 { get; set; }
        public string? fmco_acct_unit3 { get; set; }
        public string? fmco_acct_unit4 { get; set; }
        public string? vmco_acct_unit1 { get; set; }
        public string? vmco_acct_unit2 { get; set; }
        public string? vmco_acct_unit3 { get; set; }
        public string? vmco_acct_unit4 { get; set; }
        public string? wip_matl_acct_unit1 { get; set; }
        public string? wip_matl_acct_unit2 { get; set; }
        public string? wip_matl_acct_unit3 { get; set; }
        public string? wip_matl_acct_unit4 { get; set; }
        public string? wip_lbr_acct_unit1 { get; set; }
        public string? wip_lbr_acct_unit2 { get; set; }
        public string? wip_lbr_acct_unit3 { get; set; }
        public string? wip_lbr_acct_unit4 { get; set; }
        public string? wip_fovhd_acct_unit1 { get; set; }
        public string? wip_fovhd_acct_unit2 { get; set; }
        public string? wip_fovhd_acct_unit3 { get; set; }
        public string? wip_fovhd_acct_unit4 { get; set; }
        public string? wip_vovhd_acct_unit1 { get; set; }
        public string? wip_vovhd_acct_unit2 { get; set; }
        public string? wip_vovhd_acct_unit3 { get; set; }
        public string? wip_vovhd_acct_unit4 { get; set; }
        public string? wip_out_acct_unit1 { get; set; }
        public string? wip_out_acct_unit2 { get; set; }
        public string? wip_out_acct_unit3 { get; set; }
        public string? wip_out_acct_unit4 { get; set; }
        public string? muv_acct_unit1 { get; set; }
        public string? muv_acct_unit2 { get; set; }
        public string? muv_acct_unit3 { get; set; }
        public string? muv_acct_unit4 { get; set; }
        public string? lrv_acct_unit1 { get; set; }
        public string? lrv_acct_unit2 { get; set; }
        public string? lrv_acct_unit3 { get; set; }
        public string? lrv_acct_unit4 { get; set; }
        public string? luv_acct_unit1 { get; set; }
        public string? luv_acct_unit2 { get; set; }
        public string? luv_acct_unit3 { get; set; }
        public string? luv_acct_unit4 { get; set; }
        public string? fmouv_acct_unit1 { get; set; }
        public string? fmouv_acct_unit2 { get; set; }
        public string? fmouv_acct_unit3 { get; set; }
        public string? fmouv_acct_unit4 { get; set; }
        public string? vmouv_acct_unit1 { get; set; }
        public string? vmouv_acct_unit2 { get; set; }
        public string? vmouv_acct_unit3 { get; set; }
        public string? vmouv_acct_unit4 { get; set; }
        public string? flouv_acct_unit1 { get; set; }
        public string? flouv_acct_unit2 { get; set; }
        public string? flouv_acct_unit3 { get; set; }
        public string? flouv_acct_unit4 { get; set; }
        public string? vlouv_acct_unit1 { get; set; }
        public string? vlouv_acct_unit2 { get; set; }
        public string? vlouv_acct_unit3 { get; set; }
        public string? vlouv_acct_unit4 { get; set; }
        public string? fmcouv_acct_unit1 { get; set; }
        public string? fmcouv_acct_unit2 { get; set; }
        public string? fmcouv_acct_unit3 { get; set; }
        public string? fmcouv_acct_unit4 { get; set; }
        public string? vmcouv_acct_unit1 { get; set; }
        public string? vmcouv_acct_unit2 { get; set; }
        public string? vmcouv_acct_unit3 { get; set; }
        public string? vmcouv_acct_unit4 { get; set; }
        public string? cost_code { get; set; }
        public decimal? queue_hrs { get; set; }
        public byte NoteExistsFlag { get; set; }
        public DateTime RecordDate { get; set; }
        public Guid RowPointer { get; set; }
        public string? dispatch_lists_email { get; set; }
        public string? sched_drv { get; set; }
        public decimal? finish_hrs { get; set; }
        public string? setuprgid { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreateDate { get; set; }
        public byte InWorkflow { get; set; }
        public byte? Uf_insp_flag { get; set; }
        public byte? Uf_WCIncludeInLoad { get; set; }
    }


    [Table("employee_mst")]
    public class EmployeeMst
    {
        [Key]
        [Column("emp_num")]
        public string? emp_num { get; set; }

        [Column("site_ref")]
        public string? site_ref { get; set; }

        [Column("name")]
        public string? name { get; set; }

        [Column("city")]
        public string? city { get; set; }

        [Column("state")]
        public string? state { get; set; }

        [Column("zip")]
        public string? zip { get; set; }

        [Column("phone")]
        public string? phone { get; set; }

        [Column("ssn")]
        public string? ssn { get; set; }

        [Column("dept")]
        public string? dept { get; set; }

        [Column("emp_type")]
        public string? emp_type { get; set; }

        [Column("pay_freq")]
        public string? pay_freq { get; set; }

        [Column("mfg_reg_rate")]
        public decimal? mfg_reg_rate { get; set; }

        [Column("mfg_ot_rate")]
        public decimal? mfg_ot_rate { get; set; }

        [Column("mfg_dt_rate")]
        public decimal? mfg_dt_rate { get; set; }

        [Column("birth_date")]
        public DateTime? birth_date { get; set; }

        [Column("hire_date")]
        public DateTime? hire_date { get; set; }

        [Column("raise_date")]
        public DateTime? raise_date { get; set; }

        [Column("review_date")]
        public DateTime? review_date { get; set; }

        [Column("term_date")]
        public DateTime? term_date { get; set; }

        [Column("salary")]
        public decimal? salary { get; set; }

        [Column("reg_rate")]
        public decimal? reg_rate { get; set; }

        [Column("ot_rate")]
        public decimal? ot_rate { get; set; }

        [Column("dt_rate")]
        public decimal? dt_rate { get; set; }

        [Column("fwt_num")]
        public byte? fwt_num { get; set; }

        [Column("fwt_dol")]
        public decimal? fwt_dol { get; set; }

        [Column("swt_num")]
        public byte? swt_num { get; set; }

        [Column("swt_dol")]
        public decimal? swt_dol { get; set; }

        [Column("ytd_fwt")]
        public decimal? ytd_fwt { get; set; }

        [Column("ytd_swt")]
        public decimal? ytd_swt { get; set; }

        [Column("ytd_med")]
        public decimal? ytd_med { get; set; }

        [Column("ytd_tip_cr")]
        public decimal? ytd_tip_cr { get; set; }

        [Column("noteexistsflag")]
        public byte NoteExistsFlag { get; set; }

        [Column("recorddate")]
        public DateTime RecordDate { get; set; }

        [Column("rowpointer")]
        public Guid RowPointer { get; set; }

        [Column("createdby")]
        public string? CreatedBy { get; set; }

        [Column("updatedby")]
        public string? UpdatedBy { get; set; }

        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [Column("inworkflow")]
        public byte InWorkflow { get; set; }

        [Column("vac_paid")]
        public byte vac_paid { get; set; }

        [Column("sick_paid")]
        public byte sick_paid { get; set; }

        [Column("hol_paid")]
        public byte hol_paid { get; set; }

        [Column("other_paid")]
        public byte other_paid { get; set; }

        [Column("Uf_Bonus")]
        public decimal? Uf_Bonus { get; set; }

        [Column("Uf_last_updated")]
        public DateTime? Uf_last_updated { get; set; }

        [Column("Uf_new_vac_hr_due")]
        public int? Uf_new_vac_hr_due { get; set; }

        [Column("Uf_OfficeLocation")]
        public string? Uf_OfficeLocation { get; set; }

        [Column("Uf_TermReason")]
        public string? Uf_TermReason { get; set; }

        [Column("Uf_EmpExt")]
        public string? Uf_EmpExt { get; set; }

        [Column("emp_status")]
        public string? emp_status { get; set; }

        [Column("email_addr")]
        public string? email_addr { get; set; }
        public string? PasswordHash { get; set; }
        public int? RoleID { get; set; }
        public bool? IsActive { get; set; }
        public string? ProfileImage { get; set; }

    }
[Table("employee_mst")]
    public class EmployeeMstSource
    {
        [Key]
        [Column("emp_num")]
        public string? emp_num { get; set; }
 
        [Column("site_ref")]
        public string? site_ref { get; set; }
 
        [Column("name")]
        public string? name { get; set; }
 
        [Column("city")]
        public string? city { get; set; }
 
        [Column("state")]
        public string? state { get; set; }
 
        [Column("zip")]
        public string? zip { get; set; }
 
        [Column("phone")]
        public string? phone { get; set; }
 
        [Column("ssn")]
        public string? ssn { get; set; }
 
        [Column("dept")]
        public string? dept { get; set; }
 
        [Column("emp_type")]
        public string? emp_type { get; set; }
 
        [Column("pay_freq")]
        public string? pay_freq { get; set; }
 
        [Column("mfg_reg_rate")]
        public decimal? mfg_reg_rate { get; set; }
 
        [Column("mfg_ot_rate")]
        public decimal? mfg_ot_rate { get; set; }
 
        [Column("mfg_dt_rate")]
        public decimal? mfg_dt_rate { get; set; }
 
        [Column("birth_date")]
        public DateTime? birth_date { get; set; }
 
        [Column("hire_date")]
        public DateTime? hire_date { get; set; }
 
        [Column("raise_date")]
        public DateTime? raise_date { get; set; }
 
        [Column("review_date")]
        public DateTime? review_date { get; set; }
 
        [Column("term_date")]
        public DateTime? term_date { get; set; }
 
        [Column("salary")]
        public decimal? salary { get; set; }
 
        [Column("reg_rate")]
        public decimal? reg_rate { get; set; }
 
        [Column("ot_rate")]
        public decimal? ot_rate { get; set; }
 
        [Column("dt_rate")]
        public decimal? dt_rate { get; set; }
 
        [Column("fwt_num")]
        public byte? fwt_num { get; set; }
 
        [Column("fwt_dol")]
        public decimal? fwt_dol { get; set; }
 
        [Column("swt_num")]
        public byte? swt_num { get; set; }
 
        [Column("swt_dol")]
        public decimal? swt_dol { get; set; }
 
        [Column("ytd_fwt")]
        public decimal? ytd_fwt { get; set; }
 
        [Column("ytd_swt")]
        public decimal? ytd_swt { get; set; }
 
        [Column("ytd_med")]
        public decimal? ytd_med { get; set; }
 
        [Column("ytd_tip_cr")]
        public decimal? ytd_tip_cr { get; set; }
 
        [Column("noteexistsflag")]
        public byte NoteExistsFlag { get; set; }
 
        [Column("recorddate")]
        public DateTime RecordDate { get; set; }
 
        [Column("rowpointer")]
        public Guid RowPointer { get; set; }
 
        [Column("createdby")]
        public string? CreatedBy { get; set; }
 
        [Column("updatedby")]
        public string? UpdatedBy { get; set; }
 
        [Column("createdate")]
        public DateTime CreateDate { get; set; }
 
        [Column("inworkflow")]
        public byte InWorkflow { get; set; }
 
        [Column("vac_paid")]
        public byte vac_paid { get; set; }
 
        [Column("sick_paid")]
        public byte sick_paid { get; set; }
 
        [Column("hol_paid")]
        public byte hol_paid { get; set; }
 
        [Column("other_paid")]
        public byte other_paid { get; set; }
 
        [Column("Uf_Bonus")]
        public decimal? Uf_Bonus { get; set; }
 
        [Column("Uf_last_updated")]
        public DateTime? Uf_last_updated { get; set; }
 
        [Column("Uf_new_vac_hr_due")]
        public int? Uf_new_vac_hr_due { get; set; }
 
        [Column("Uf_OfficeLocation")]
        public string? Uf_OfficeLocation { get; set; }
 
        [Column("Uf_TermReason")]
        public string? Uf_TermReason { get; set; }
 
        [Column("Uf_EmpExt")]
        public string? Uf_EmpExt { get; set; }
 
        [Column("emp_status")]
        public string? emp_status { get; set; }
 
        [Column("email_addr")]
        public string? email_addr { get; set; }
    }
   
 
    [Table("jobtran_mst")]
    public class JobTranMst
    {
        [Column(Order = 0)]
        [Required]
        [StringLength(8)]
        public string? site_ref { get; set; }

        [Key]
        [Required]
        [Column("trans_num", Order = 1)]
        public decimal trans_num { get; set; }

        [StringLength(20)]
        public string? job { get; set; }

        public short? suffix { get; set; }

        [StringLength(1)]
        public string? trans_type { get; set; }

        public DateTime? trans_date { get; set; }

        //  [Column(TypeName = "decimal(19,8)")]
        public decimal? qty_complete { get; set; }

        //  [Column(TypeName = "decimal(19,8)")]
        public decimal? qty_scrapped { get; set; }

        public int? oper_num { get; set; }

        //   [Column(TypeName = "decimal(19,8)")] 
        public decimal? a_hrs { get; set; }

        public int? next_oper { get; set; }

        [StringLength(7)]
        public string? emp_num { get; set; }

        [Column("a_$")]
        public decimal? a_dollar { get; set; }

        public DateTime? start_time { get; set; }
        public DateTime? end_time { get; set; }

        [StringLength(3)]
        public string? ind_code { get; set; }

        [StringLength(1)]
        public string? pay_rate { get; set; }

        //  [Column(TypeName = "decimal(19,8)")]
        public decimal? qty_moved { get; set; }

        [StringLength(4)]
        public string? whse { get; set; }

        [StringLength(15)]
        public string? loc { get; set; }

        [StringLength(3)]
        public string? user_code { get; set; }

        public byte? close_job { get; set; }

        public byte? issue_parent { get; set; }

        [StringLength(15)]
        public string? lot { get; set; }

        public byte? complete_op { get; set; }

        //  [Column(TypeName = "decimal(9,3)")]
        public decimal? pr_rate { get; set; }

        // [Column(TypeName = "decimal(9,3)")]
        public decimal? job_rate { get; set; }

        [StringLength(3)]
        public string? shift { get; set; }

        [Required]
        public byte? posted { get; set; }

        public byte? low_level { get; set; }

        public byte? backflush { get; set; }

        [StringLength(3)]
        public string? reason_code { get; set; }

        [StringLength(1)]
        public string? trans_class { get; set; }

        [StringLength(10)]
        public string? ps_num { get; set; }

        [StringLength(6)]
        public string? wc { get; set; }

        public byte? awaiting_eop { get; set; }

        //  [Column(TypeName = "decimal(20,8)")]
        public decimal? fixovhd { get; set; }

        //  [Column(TypeName = "decimal(20,8)")]
        public decimal? varovhd { get; set; }

        [StringLength(3)]
        public string? cost_code { get; set; }

        public byte? co_product_mix { get; set; }

        [Required]
        public byte NoteExistsFlag { get; set; }

        [Required]
        public DateTime RecordDate { get; set; }

        [Required]
        public Guid RowPointer { get; set; }

        [Required]
        [StringLength(128)]
        public string? CreatedBy { get; set; }

        [Required]
        [StringLength(128)]
        public string? UpdatedBy { get; set; }

        [Required]
        public DateTime CreateDate { get; set; }

        [Required]
        public byte InWorkflow { get; set; }

        [StringLength(25)]
        public string? import_doc_id { get; set; }

        [StringLength(15)]
        public string? container_num { get; set; }

        [StringLength(15)]
        public string? parent_lot { get; set; }

        [StringLength(30)]
        public string? parent_serial { get; set; }

        [StringLength(30)]
        public string? RESID { get; set; }

        public byte? Uf_MovedOKToStock { get; set; }

        public byte? Uf_OperCompleted { get; set; }

        [StringLength(60)]
        public string? Uf_CustName { get; set; }

        [StringLength(260)]
        public string? Uf_ItemDescription2 { get; set; }

        [StringLength(20)]
        public string? Uf_serial { get; set; }
        public string? machine_id { get; set; }
        public bool? completed_flag { get; set; }
        public string? SerialNo { get; set; }
        public string? status { get; set; }
        public string? qcgroup { get; set; }
        public string? item { get; set; }
        public string? Remark { get; set; }
    }

    public class JobPoolRequest
    {
        public string JobPoolNumber { get; set; } = string.Empty;
    }


    [Table("job_sch_mst")]
    public class JobSchMst
    {
        [Key]
        [Column("job", Order = 0)]
        [StringLength(20)]
        public string? Job { get; set; }

        [Column("suffix", Order = 1)]
        public short Suffix { get; set; }

        [Column("site_ref", Order = 2)]
        [StringLength(8)]
        public string? SiteRef { get; set; }

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("start_tick")]
        public decimal? StartTick { get; set; }

        [Column("end_tick")]
        public decimal? EndTick { get; set; }

        [Column("priority")]
        public short? Priority { get; set; }

        [Column("prfreeze")]
        public byte? Prfreeze { get; set; }

        [Column("sequence")]
        [StringLength(3)]
        public string? Sequence { get; set; }

        [Required]
        [Column("NoteExistsFlag")]
        public byte NoteExistsFlag { get; set; }

        [Required]
        [Column("RecordDate")]
        public DateTime RecordDate { get; set; }

        [Required]
        [Column("RowPointer")]
        public Guid RowPointer { get; set; }

        [Column("compdate")]
        public DateTime? CompDate { get; set; }

        [Required]
        [Column("CreatedBy")]
        [StringLength(128)]
        public string? CreatedBy { get; set; }

        [Required]
        [Column("UpdatedBy")]
        [StringLength(128)]
        public string? UpdatedBy { get; set; }

        [Required]
        [Column("CreateDate")]
        public DateTime CreateDate { get; set; }

        [Required]
        [Column("InWorkflow")]
        public byte InWorkflow { get; set; }

        [Column("compdate_day")]
        public DateTime? CompDateDay { get; set; }

        [Column("end_date_day")]
        public DateTime? EndDateDay { get; set; }

        [Column("published_start_date")]
        public DateTime? PublishedStartDate { get; set; }
    }


    public class JobDetailsDto
    {
        public JobMst? Job { get; set; }
        public List<JobTranMst>? Transactions { get; set; }
        public List<EmployeeMst>? Employees { get; set; }
        public List<JobmatlMst>? Materials { get; set; }
        public List<JobSchMst>? Schedules { get; set; }
        public List<JobRouteMst>? Routes { get; set; }
        public List<WcMst>? WorkCenters { get; set; }
    }


    public class JobDto
    {
        public string? job { get; set; }
    }


    public class JobOperationDetailsDto
    {
        public string? TransType { get; set; }
        public DateTime? TransDate { get; set; }
        public string? Job { get; set; }
        public int Suffix { get; set; }
        public string? Item { get; set; }
        public string? ItemDescription { get; set; }
        public string? WC { get; set; }
        public string? WC_Description { get; set; }
        public int? NextOper { get; set; }
        public string? NextOperWC { get; set; }
        public string? NextOperWC_Description { get; set; }
        public decimal? QtyComplete { get; set; }
        public decimal? QtyMoved { get; set; }
        public decimal? QtyScrapped { get; set; }
        public int? StartTime { get; set; }
        public int? EndTime { get; set; }
        public double? TotalHour { get; set; }
        public int? Sequence { get; set; }
        public string? MatlType { get; set; }
        public decimal? QtyIssued { get; set; }
        public decimal? ScrapFact { get; set; }
        public string? EmpNum { get; set; }
        public string? Name { get; set; }
        public string? Dept { get; set; }
        public int? MachineNumber { get; set; }
        public string? MachineName { get; set; }
        public string? MachineDescription { get; set; }
        public decimal? Yield { get; set; }
        public byte? Backflush { get; set; }
        public int OperNum { get; set; }

    }


    public class JobOperationRequestDto
    {
        public string Job { get; set; } = string.Empty;
        public int OperNum { get; set; }
    }


    public class Calendar
    {
        public int ID { get; set; }

        [Required]
        public DateTime date { get; set; }

        [Required]
        [Range(0, 1)]
        public int flag { get; set; }  // 0 = Overtime, 1 = Double Time 

        public string? CalendarDescription { get; set; }

        public string? Occasion { get; set; }

    }

    public class OperationDto
    {
        public int OperationNumber { get; set; }
        public string? wc { get; set; }
        public string? wc_description { get; set; }
        public string? OperationName { get; set; }
        public string? OperationDescription { get; set; }
    }

    public class QcJobDto
    {
        public string Job { get; set; } = null!;
        public int OperNum { get; set; }
        public string? Wc { get; set; }
        public string? EmpNum { get; set; }
    }

    [Table("item_mst")]
    public class ItemMst
    {
        public string? site_ref { get; set; }
        [Key]
        public string? item { get; set; }
        public string? description { get; set; }
        public decimal? Qty_AllocJob { get; set; }
        public string? U_M { get; set; }
        public short? Lead_Time { get; set; }
        public decimal? Lot_Size { get; set; }
        public decimal? Qty_Used_Ytd { get; set; }
        public decimal? Qty_Mfg_Ytd { get; set; }
        public string? Abc_Code { get; set; }
        public string? Drawing_Nbr { get; set; }
        public string? Product_Code { get; set; }
        public string? P_M_T_Code { get; set; }
        public string? Cost_Method { get; set; }
        public decimal? Lst_Lot_Size { get; set; }
        public decimal? Unit_Cost { get; set; }
        public decimal? Lst_U_Cost { get; set; }
        public decimal? Avg_U_Cost { get; set; }
        public string? job { get; set; }
        public short? Suffix { get; set; }
        public byte? Stocked { get; set; }
        public string? Matl_Type { get; set; }
        public string? Family_Code { get; set; }
        public byte? Low_Level { get; set; }
        public DateTime? Last_Inv { get; set; }
        public short? Days_Supply { get; set; }
        public decimal? Order_Min { get; set; }
        public decimal? Order_Mult { get; set; }
        public string? Plan_Code { get; set; }
        public byte? Mps_Flag { get; set; }
        public byte? Accept_Req { get; set; }
        public DateTime? Change_Date { get; set; }
        public string? Revision { get; set; }
        public byte? Phantom_Flag { get; set; }
        public byte? Plan_Flag { get; set; }
        public short? Paper_Time { get; set; }
        public short? Dock_Time { get; set; }
        public decimal? Asm_Setup { get; set; }
        public decimal? Asm_Run { get; set; }
        public decimal? Asm_Matl { get; set; }
        public decimal? Asm_Tool { get; set; }
        public decimal? Asm_Fixture { get; set; }
        public decimal? Asm_Other { get; set; }
        public decimal? Asm_Fixed { get; set; }
        public decimal? Asm_Var { get; set; }
        public decimal? Asm_Outside { get; set; }
        public decimal? Comp_Setup { get; set; }
        public decimal? Comp_Run { get; set; }
        public decimal? Comp_Matl { get; set; }
        public decimal? Comp_Tool { get; set; }
        public decimal? Comp_Fixture { get; set; }
        public decimal? Comp_Other { get; set; }
        public decimal? Comp_Fixed { get; set; }
        public decimal? Comp_Var { get; set; }
        public decimal? Comp_Outside { get; set; }
        public decimal? Sub_Matl { get; set; }
        public decimal? Shrink_Fact { get; set; }
        public string? Alt_Item { get; set; }
        public decimal? Unit_Weight { get; set; }
        public string? Weight_Units { get; set; }
        public string? Charfld4 { get; set; }
        public decimal? Cur_U_Cost { get; set; }
        public string? Feat_Type { get; set; }
        public decimal? Var_Lead { get; set; }
        public string? Feat_Str { get; set; }
        public short? Next_Config { get; set; }
        public string? Feat_Templ { get; set; }
        public byte? Backflush { get; set; }
        public string? Charfld1 { get; set; }
        public string? Charfld2 { get; set; }
        public string? Charfld3 { get; set; }
        public decimal? Decifld1 { get; set; }
        public decimal? Decifld2 { get; set; }
        public decimal? Decifld3 { get; set; }
        public byte? Logifld { get; set; }
        public DateTime? Datefld { get; set; }
        public byte? Track_Ecn { get; set; }
        public decimal? U_Ws_Price { get; set; }
        public string? Comm_Code { get; set; }
        public string? Origin { get; set; }
        public decimal? Unit_Mat_Cost { get; set; }
        public decimal? Unit_Duty_Cost { get; set; }
        public decimal? Unit_Freight_Cost { get; set; }
        public decimal? Unit_Brokerage_Cost { get; set; }
        public decimal? Cur_Mat_Cost { get; set; }
        public decimal? Cur_Duty_Cost { get; set; }
        public decimal? Cur_Freight_Cost { get; set; }
        public decimal? Cur_Brokerage_Cost { get; set; }
        public string? Tax_Code1 { get; set; }
        public string? Tax_Code2 { get; set; }
        public string? Bflush_Loc { get; set; }
        public byte? Reservable { get; set; }
        public short? Shelf_Life { get; set; }
        public string? Lot_Prefix { get; set; }
        public string? Serial_Prefix { get; set; }
        public byte? Serial_Length { get; set; }
        public string? Issue_By { get; set; }
        public byte? Serial_Tracked { get; set; }
        public byte? Lot_Tracked { get; set; }
        public string? Cost_Type { get; set; }
        public decimal? Matl_Cost { get; set; }
        public decimal? Lbr_Cost { get; set; }
        public decimal? Fovhd_Cost { get; set; }
        public decimal? Vovhd_Cost { get; set; }
        public decimal? Out_Cost { get; set; }
        public decimal? Cur_Matl_Cost { get; set; }
        public decimal? Cur_Lbr_Cost { get; set; }
        public decimal? Cur_Fovhd_Cost { get; set; }
        public decimal? Cur_Vovhd_Cost { get; set; }
        public decimal? Cur_Out_Cost { get; set; }
        public decimal? Avg_Matl_Cost { get; set; }
        public decimal? Avg_Lbr_Cost { get; set; }
        public decimal? Avg_Fovhd_Cost { get; set; }
        public decimal? Avg_Vovhd_Cost { get; set; }
        public decimal? Avg_Out_Cost { get; set; }
        public string? Prod_Type { get; set; }
        public decimal? Rate_Per_Day { get; set; }
        public short? Mps_Plan_Fence { get; set; }
        public byte? Pass_Req { get; set; }
        public byte? Lot_Gen_Exp { get; set; }
        public string? Supply_Site { get; set; }
        public string? Prod_Mix { get; set; }
        public string? Stat { get; set; }
        public string? Status_Chg_User_Code { get; set; }
        public DateTime? Chg_Date { get; set; }
        public string? Reason_Code { get; set; }
        public string? Supply_Whse { get; set; }
        public short? Due_Period { get; set; }
        public decimal? Order_Max { get; set; }
        public byte? Mrp_Part { get; set; }
        public byte? Infinite_Part { get; set; }
        public byte? NoteExistsFlag { get; set; }
        public DateTime? RecordDate { get; set; }
        public Guid? RowPointer { get; set; }
        public decimal? Supply_Tolerance_Hrs { get; set; }
        public short? Exp_Lead_Time { get; set; }
        public decimal? Var_Exp_Lead { get; set; }
        public string? Buyer { get; set; }
        public byte? Order_Configurable { get; set; }
        public byte? Job_Configurable { get; set; }
        public string? Cfg_Model { get; set; }
        public string? Co_Post_Config { get; set; }
        public string? Job_Post_Config { get; set; }
        public string? Auto_Job { get; set; }
        public string? Auto_Post { get; set; }
        public string? SetupGroup { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? CreateDate { get; set; }
        public byte? InWorkflow { get; set; }
        public byte? Mfg_Supply_Switching_Active { get; set; }
        public short? Time_Fence_Rule { get; set; }
        public double? Time_Fence_Value { get; set; }
        public DateTime? Earliest_Planned_Po_Receipt { get; set; }
        public byte? Use_Reorder_Point { get; set; }
        public decimal? Reorder_Point { get; set; }
        public decimal? Fixed_Order_Qty { get; set; }
        public decimal? Unit_Insurance_Cost { get; set; }
        public decimal? Unit_Loc_Frt_Cost { get; set; }
        public decimal? Cur_Insurance_Cost { get; set; }
        public decimal? Cur_Loc_Frt_Cost { get; set; }
        public byte? Tax_Free_Matl { get; set; }
        public short? Tax_Free_Days { get; set; }
        public decimal? Safety_Stock_Percent { get; set; }
        public string? Tariff_Classification { get; set; }
        public DateTime? LowDate { get; set; }

        // --- UF (User Fields)
        public DateTime? Uf_BOMApprovedByDate { get; set; }
        public DateTime? Uf_BOMPreparedByDate { get; set; }
        public DateTime? Uf_BOMRevDate { get; set; }
        public DateTime? Uf_BOMSendForApprovalDate { get; set; }
        public DateTime? Uf_DrawingRevisionDate { get; set; }
        public DateTime? Uf_ItemRevDate { get; set; }
        public byte? Uf_ItemSpare { get; set; }
        public string? Uf_ItemDescription2 { get; set; }
        public int? Uf_ItemSpecRevision { get; set; }
        public string? Uf_ItemStatus { get; set; }
        public string? Uf_ItemTemp { get; set; }
        public string? Uf_LastECN { get; set; }
        public string? Uf_Model { get; set; }
        public string? Uf_PreparedBy { get; set; }
        public string? Uf_SpecNo { get; set; }
        public string? Uf_TagType { get; set; }
        public string? Uf_ToBeApprovedBy { get; set; }
        public string? Uf_ToBeCheckedBy { get; set; }

    }

    [Table("wom_wc_employee")]
    public class WomWcEmployee
    {
        [Key]
        [Column("wc", Order = 0)]
        [StringLength(6)]
        public string Wc { get; set; } = null!;

        [Column("emp_num", Order = 1)]
        [StringLength(7)]
        public string EmpNum { get; set; } = null!;

        [StringLength(40)]
        [Column("description")]
        public string? Description { get; set; }

        [StringLength(50)]
        [Column("name")]
        public string? Name { get; set; }

        [Column("NoteExistsFlag")]
        public byte NoteExistsFlag { get; set; }

        [Column("RecordDate")]
        public DateTime RecordDate { get; set; }

        [Column("RowPointer")]
        public Guid RowPointer { get; set; }

        [Required]
        [StringLength(128)]
        [Column("CreatedBy")]
        public string CreatedBy { get; set; } = null!;

        [Required]
        [StringLength(128)]
        [Column("UpdatedBy")]
        public string UpdatedBy { get; set; } = null!;

        [Column("CreateDate")]
        public DateTime CreateDate { get; set; }

        [Column("InWorkflow")]
        public byte InWorkflow { get; set; }

    }

    public class JobReportDto
    {
        public string? Job { get; set; }
        public DateTime? JobDate { get; set; }
        public string? PreparedBy { get; set; }
        public string? MaterialClass { get; set; }
        public string? DrawingNo { get; set; }
        public string? RevisionNo { get; set; }
        public string? TempClass { get; set; }
        public int? DrawingRev { get; set; }
        public string? SoNo { get; set; }
        public decimal? ReleasedQty { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? Status { get; set; }
        public string? MatlDesc { get; set; }
        public string? PSL { get; set; }
        public string? Item { get; set; }
        public string? ItemDescription { get; set; }
        public DateTime? JobDueDate { get; set; }
        public int? Suffix { get; set; }
        public string? UfItemDescription2 { get; set; }
        public List<JobOperationDto> Operations { get; set; } = new();
    }

    public class JobOperationDto
    {
        public int OperNum { get; set; }
        public int Sequence { get; set; }
        public string? OperationDescription { get; set; } = string.Empty;
        public List<JobOperationItemDto> Items { get; set; } = new();

        // new property
        //   public JobTransactionDto? Transaction { get; set; }
        public List<JobTransactionDto>? Transactions { get; set; }
    }
  public class JobTransactionDto
{
    public string? SerialNo { get; set; }  
     public DateTime CreateDate { get; set; }        // Serial number
    public DateTime? TransDate { get; set; }       // Transaction date
    public string? TransType { get; set; }         // Start, Pause, Complete, etc.
    public bool Posted { get; set; }               // Posted flag
    public string? Status { get; set; }            // Status (completed, pending, etc.)
    public decimal? QtyComplete { get; set; }      // Completed qty
    public decimal? QtyScrapped { get; set; }      // Scrapped qty
    public string? Remark { get; set; }            // Remarks/comments
    public string? Item { get; set; }              // Item code
    public string? UfItemDescription2 { get; set; } // Item description
}


    public class JobOperationItemDto
    {
        public string? Item { get; set; }
        public string? ItemDescription { get; set; }
        public decimal? RequiredQty { get; set; }
        public int? Sequence { get; set; }
        public string UfLastVendName { get; set; } = string.Empty;
        public string? UfItemDescription2 { get; set; }   // from item_mst

    }

    public class EmployeeDto
    {
        public string? EmpNum { get; set; }        // Primary key
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }
        public int? RoleID { get; set; }
        public bool? IsActive { get; set; }
        public string? ProfileImage { get; set; }
        public string? site_ref { get; set; }
        public string? CreatedBy { get; set; }
        public string? depart { get; set; }
        public string? emp_type { get; set; }
        public string? pay_freq { get; set; }
        public decimal mfg_reg_rate { get; set; }
        public decimal mfg_ot_rate { get; set; }
        public decimal mfg_dt_rate { get; set; }
    }

    public class JobOperationRequest
    {
        public string Job { get; set; } = default!;
        public int OperNum { get; set; }

    }

    public class JobOperationDetailsDtos
    {
        public string? Job { get; set; }
        public int OperNum { get; set; }
        public decimal ReleasedQty { get; set; }
        public string? WorkCenter { get; set; }
        public int? suffix { get; set; }
        public List<EmployeesDto>? Employees { get; set; }
    }

    public class EmployeesDto
    {
        public string? EmpNum { get; set; }
        public string? Name { get; set; }
        public decimal? TransNum { get; set; }
        public string? wc { get; set; }
        public List<string> Machines { get; set; } = new(); // new property

    }

    [Table("wom_machine_employee")]
    public class WomMachineEmployee
    {

        [Column("MachineNumber")]  //  map to actual DB column
        public string Machine_Num { get; set; } = null!;

        [Column("emp_num")]
        public string Emp_Num { get; set; } = null!;

        [Column("MachineDescription")]  //  map to actual DB column
        public string? Machine_Description { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("noteexistsflag")]
        public byte NoteExistsFlag { get; set; }

        [Column("recorddate")]
        public DateTime? RecordDate { get; set; }

        [Column("rowpointer")]
        public Guid RowPointer { get; set; }

        [Column("createdby")]
        public string CreatedBy { get; set; } = null!;

        [Column("updatedby")]
        public string UpdatedBy { get; set; } = null!;

        [Column("createdate")]
        public DateTime? CreateDate { get; set; }

        [Column("inworkflow")]
        public byte InWorkflow { get; set; }
    }

    public class MachineEmployeeDto
    {
        public string MachineNumber { get; set; } = string.Empty;
        public string MachineDescription { get; set; } = string.Empty;
        public string Emp_Num { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class JobPool
    {
        public int JobPoolID { get; set; }       // Primary Key
        public string JobPoolNumber { get; set; } = string.Empty;
        public string Job { get; set; } = string.Empty;
        public string TransactionNum { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string? Employee { get; set; }
        public string? WorkCenter { get; set; }
        public string? Machine { get; set; }
        public decimal Qty { get; set; }
        public string? Item { get; set; }

        public int Status_ID { get; set; }       // 0=Not Started,1=Running,2=Completed,...
        public DateTime Status_Time { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }



    // public class WorkCenterStatsDto
    // {
    //     public int ActiveWorkCenters { get; set; }
    //     public int TotalWorkCenters { get; set; }
    //     public string Display { get; set; } = string.Empty;
    // }

    // public class CalendarCountsDto
    // {
    //     public int OvertimeCount { get; set; }
    //     public int DoubletimeCount { get; set; }
    // }

    // public class JobtranMst
    // {
    //     public string? Job { get; set; }
    //     public decimal Qty_Released { get; set; }
    //     public decimal Qty_Complete { get; set; }
    //     public decimal Qty_Scrapped { get; set; }
    // }

    public class EmployeeNameDto
    {
        public string? EmpName { get; set; }
        public string? EmpNum { set; get; }
    }


    public class MachineNameDto
    {
        public string? MachineName { get; set; }
        public string? MachineNumber { get; set; }
    }

    public class MachineEmployeeDtos
    {
        public string MachineName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;  // Employee Name
        public string? MachineDescription { get; set; }
    }


    public class UnpostedTransactionDto
    {
        public int trans_number { get; set; }
        public string SerialNo { get; set; } = string.Empty;
        public string Job { get; set; } = string.Empty;
        public string TransType { get; set; } = string.Empty;
        public decimal QtyReleased { get; set; }
        public string Item { get; set; } = string.Empty;
        public int JobYear { get; set; }
        public string OperNum { get; set; } = string.Empty;
        public string WcCode { get; set; } = string.Empty;
        public string WcDescription { get; set; } = string.Empty;
        public string EmpNum { get; set; } = string.Empty;
        public string EmpName { get; set; } = string.Empty;
        public string MachineId { get; set; } = string.Empty;
        public string MachineDescription { get; set; } = string.Empty;
        public string QcGroup { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsNextJobActive { get; set; }

    }

    public class NextJobActiveDto
    {
        public string Job { get; set; }
        public string SerialNo { get; set; }
        public string Wc { get; set; }
        public int OperNum { get; set; }
        public int? NextOper { get; set; }
        public bool IsNextJobActive { get; set; }
    }

    [Table("wom_wc_machine")]
    public class WomWcMachine
    {
        [Key]
        public Guid RowPointer { get; set; }  // Add primary key

        [Column("wc")]
        public string Wc { get; set; } = string.Empty;

        [Column("machine_id")]
        public string MachineId { get; set; } = string.Empty;

        [Column("m_description")]
        public string MachineDescription { get; set; } = string.Empty;

        [Column("wc_name")]
        public string WcName { get; set; } = string.Empty;

        [Column("NoteExistsFlag")]
        public byte NoteExistsFlag { get; set; }

        [Column("RecordDate")]
        public DateTime RecordDate { get; set; }

        [Column("CreatedBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [Column("UpdatedBy")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("CreateDate")]
        public DateTime? CreateDate { get; set; }

        [Column("InWorkFlow")]
        public byte InWorkflow { get; set; }
    }





    public class EmployeeGetDto
    {
        public string EmpNum { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class MachineGetDto
    {
        public string MachineId { get; set; } = string.Empty;
        public string MachineDescription { get; set; } = string.Empty;
    }


    public class MachineWcDto
    {
        public string? wc { get; set; }
        public string? wcName { get; set; }
        public string? machineNumber { get; set; }
        public string? machineDescription { get; set; }
    }

public class DeleteJobTransactionDto
    {
        public string? jobNumber { get; set; }
        public string? serialNo { get; set; }
        public int oper_num { get; set; }
    }  

    public class StartGroupQCJobRequestDto
    {
        public List<StartJobRequestDto> Jobs { get; set; } = new();
    }

    public class EmployeeDtos
    {
        public string? EmpNum { get; set; }
        public string? Name { get; set; }
    }

    public class EmployeeWcDto
    {
        public string? Wc { get; set; }
        public string? EmpNum { get; set; }
        public string? Description { get; set; }  // optional
        public string? Name { get; set; }         // optional
    }


    public class CompletedQCJobDto
    {
        public string? JobNumber { get; set; }
        public string? SerialNo { get; set; }
        public int? OperationNumber { get; set; }
        public string? Wc { get; set; }
        public string? EmployeeCode { get; set; }
        public string? Item { get; set; }
        public decimal QtyReleased { get; set; }
        public string? Status { get; set; }
        public DateTime TransDate { get; set; }
        public decimal TotalHours { get; set; }
        public int TransNum { get; set; }
        public DateTime EndTime { get; set; }  // current time when returning completed job
    }

    public class JobTran
    {
        public string? JobNumber { get; set; }
        public string? SerialNo { get; set; }
        public int OperNum { get; set; }
        public int Status { get; set; }   // 1 = Running, 2 = Paused, 3 = Completed
        public string? Remark { get; set; } // ✅ Add this line
                                            // other fields...
    }

    public class QCRemarkUpdateDto
    {
        public int trans_num { get; set; }
        public string? Remark { get; set; }
    }

    public class TransactionOverviewRequest
    {
        public int todayOnly { get; set; }
        public int includeTransaction { get; set; }
        public int includeQC { get; set; }
    }


    public class JobOverviewFilterDto
    {
        public int TodayOnly { get; set; }
        public int IncludeTransaction { get; set; }
        public int IncludeQC { get; set; }
          public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    }

public class ScrapJobDto
{
    public string JobNumber { get; set; }
    public string SerialNo { get; set; }
    public int OperationNumber { get; set; }
    public string LoginUser { get; set; }
}

}


