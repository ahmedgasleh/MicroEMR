using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientClinicalHistory;
public sealed class PatientClinicalHistoryViewModel{public Guid HistoryUid{get;set;}public Guid PatientUid{get;set;}public string HistoryType{get;set;}="";public string Description{get;set;}="";public DateOnly?RelevantDate{get;set;}public string Status{get;set;}="";public DateTime CreatedAt{get;set;}public string?CreatedByDisplayName{get;set;}public DateTime?UpdatedAt{get;set;}public string?UpdatedByDisplayName{get;set;}public string RowVersion{get;set;}="";}
public class SavePatientClinicalHistoryViewModel{public Guid PatientUid{get;set;}[Required,StringLength(20)]public string HistoryType{get;set;}="";[Required,StringLength(1000)]public string Description{get;set;}="";public DateOnly?RelevantDate{get;set;}public string?RowVersion{get;set;}}
public sealed class UpdatePatientClinicalHistoryViewModel:SavePatientClinicalHistoryViewModel{public Guid HistoryUid{get;set;}}
