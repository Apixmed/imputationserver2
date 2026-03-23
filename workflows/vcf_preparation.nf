
include { COMPRESS } from '../modules/local/vcf_prep/vcf_prep'
include { INDEX } from '../modules/local/vcf_prep/vcf_prep'
include { MERGE } from '../modules/local/vcf_prep/vcf_prep'
include { SPLIT_CHR } from '../modules/local/vcf_prep/vcf_prep'
include { FIX_CHR_X } from '../modules/local/vcf_prep/vcf_prep'

workflow VCF_PREP {

    main:

    params.prepped_vcfs = "${file(params.files).first().parent}/imputation_prepped"
    input_ch = Channel.fromPath(params.files)
    // Split input VCFs into gzipped and not gzipped
    input_ch.branch {
        to_compress: !it.name.endsWith('.gz')
        already_compressed: it.name.endsWith('.gz')
    }
    .set { split_input_ch }

    // Compress the uncompressed files
    COMPRESS(split_input_ch.to_compress)

    // Combine all the VCFs (now all compressed)
    all_vcf_gz_ch = COMPRESS.out.gz_vcf.mix(split_input_ch.already_compressed)

    // Index everything
    INDEX(all_vcf_gz_ch)

    // Merge all the compressed files and produce multisample VCF
    MERGE(all_vcf_gz_ch.collect(), INDEX.out.index.collect(), file(params.rename_chr_map))

    // Split multisample VCF by chromosome
    SPLIT_CHR(Channel.from(1..22), MERGE.out)

    // Fix ploidy in chrX.vcf
    FIX_CHR_X(MERGE.out, file(params.ploidy_file))
    prepped_vcfs = SPLIT_CHR.out.concat(FIX_CHR_X.out)
    
    emit:
    prepped_vcfs
}
