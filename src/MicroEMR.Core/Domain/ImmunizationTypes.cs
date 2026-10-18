namespace MicroEMR.Core.Domain;

public sealed record ImmunizationType(string Code, string Name, string SourceStatus, string Comments, int SourceRow)
{
    public string Display => Name + (SourceStatus == "R" ? " (retired in source)" : "");
}

// CDS-S Data Dictionary v2.1 Final, published 2026-06-05, Code Tables B231:F277/B279:F289.
// Version-pinned source identity, not a live NVC catalogue or a statement of release applicability.
public static class ImmunizationTypes
{
    public const string SourceVersion = "CDS-S 2.1 Final / 2026-06-05 / CT-020";
    public static IReadOnlyList<ImmunizationType> All { get; } = Array.AsReadOnly(new ImmunizationType[]
    {
        new("BCG","Bacillus Calmette-Gu\u00e9rin","R","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\".",231),
        new("Chol-O","Cholera - Oral","R","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\"",232),
        new("CholEcol","Cholera, Enterotoxigenic Escherichia coli","P","\u00a0",233),
        new("Chol-Ecol-O","Cholera - E.coli - Oral","P","\u00a0",234),
        new("DTaP","Diphtheria, Tetanus, Acellular Pertussis - pediatric","R","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\"",235),
        new("DTaP-IPV","Diphtheria, Tetanus, Acellular Pertussis, Inactivated Polio - pediatric","P","\u00a0",236),
        new("DTaP-IPV-Hib","Diphtheria, Tetanus, Acellular Pertussis, Inactivated Polio, Haemophilus influenzae type b - pediatric","P","\u00a0",237),
        new("DTaP-IPV-Hib-HB","Diphtheria, Tetanus, Acellular Pertussis, Inactivated Polio, Haemophilus influenzae type b, Hepatitis B - pediatric","P","\u00a0",238),
        new("DTaP-IPV-HB","Diphtheria, Tetanus, Acellular Pertussis, Inactivated Polio, Hepatitis B - pediatric","R","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\"",239),
        new("DTaP-Hib","Diphtheria, Tetanus, Acellular Pertussis, Haemophilus influenzae type b - pediatric","R","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\"",240),
        new("DT-IPV","Diphtheria, Tetanus, Polio - pediatric","R","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\"",241),
        new("HA","Hepatitis A","P","\u00a0",242),
        new("HAHB","Hepatitis A and B","P","\u00a0",243),
        new("HA-Typh-I","Hepatitis A and Typhoid - Injection","P","\u00a0",244),
        new("HB","Hepatitis B","P","\u00a0",245),
        new("HBTmf","Hepatitis B - Thimerosal free","R","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\"",246),
        new("Hib","Haemophilus influenzae type b","P","\u00a0",247),
        new("HPV","Human Papilloma Virus","P","Previously categorized under \"Other\"",248),
        new("Inf","Influenza","P","\u00a0",249),
        new("IPV","Inactivated Polio","P","\u00a0",250),
        new("JE","Japanese Encephalitis","P","\u00a0",251),
        new("Men","Meningococcus","P","\u00a0",252),
        new("Men B","Neisseria Meningitidis Serogroup B Protein","P","\u00a0",253),
        new("Men-C","Meningococcal - Conjugate","P","\u00a0",254),
        new("Men-P-AC","Meningococcal - Polysaccharide","P","\u00a0",255),
        new("Men-P-ACWY","Meningococcal - Polysaccharide","P","\u00a0",256),
        new("MMR","Measles, Mumps, Rubella","P","\u00a0",257),
        new("MMR-Var","Measles, Mumps, Rubella, Varicella","P","Previously categorized under \"Other\"",258),
        new("MR","Measles, Rubella","R","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\"",259),
        new("Pneu","Pneumococcus","P","\u00a0",260),
        new("Pneu-C-7","Pneumococcal-Conjugate - valent","P","\u00a0",261),
        new("Pneu-P-23","Pneumococcal-Polysaccharide - valent","P","\u00a0",262),
        new("Rab","Rabies","P","\u00a0",263),
        new("ROT","Rotavirus - Oral","P","Previously categorized under \"Other\"",264),
        new("T","Tetanus","P","\u00a0",265),
        new("Td","Tetanus, Diphtheria - adult","P","\u00a0",266),
        new("Tdap","Tetanus, Diphtheria, Acellular Pertussis - adult","P","\u00a0",267),
        new("TdapIPV","Tetanus, Diphtheria, Acellular Pertussis, Inactivated Poliomyelitis","P","\u00a0",268),
        new("Td-IPV","Tetanus, Diphtheria, Inactivated Polio - adult","P","\u00a0",269),
        new("TBE","Tickborne Encephalitis","R","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\"",270),
        new("Typh","Typhoid","P","\u00a0",271),
        new("Typh-I","Typhoid - Injection","P","\u00a0",272),
        new("Typh-O","Typhoid - Oral","P","\u00a0",273),
        new("Typh (HA)","Typhoid, Hepatitis A","P","\u00a0",274),
        new("Var","Varicella","P","\u00a0",275),
        new("YF","Yellow Fever","P","\u00a0",276),
        new("Zos","Herpes Zoster","P","\u00a0",277),
        new("BAtx","Botulism Antitoxin","P","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\"",279),
        new("CMVIg","Cytomegalovirus Immune Globulin","P","\u00a0",280),
        new("DAtx","Diphtheria Antitoxin","P","\u00a0",281),
        new("HBIg","Hepatitis B Immunoglobulin","P","\u00a0",282),
        new("Ig","Immune Globulin","P","\u00a0",283),
        new("RabIg","Rabies Immunoglobulin","P","\u00a0",284),
        new("RSVAb","Respiratory Syncytial Virus Antibody","P","New",285),
        new("RSVIg","Respiratory Syncytial Virus Immunoglobulin","P","Not found in the February 2016 update to the \"Contents of Immunizing Agents Available for Use in Canada\" in the \"Canadian Immunization Guide\"",286),
        new("TIg","Tetanus Immunoglobulin","P","\u00a0",287),
        new("VarIg","Varicella Immunoglobulin","P","\u00a0",288),
        new("VIG","Vaccinia Immune Globulin","P","New",289),
    });
    public static bool IsValid(string? code) => code is null || All.Any(x => string.Equals(x.Code, code, StringComparison.Ordinal));
    public static string Display(string? code) => code is null ? "Not recorded" : All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.Ordinal))?.Display ?? code;
}
