
process COMPRESS {

    input:
    path vcf

    output:
    path "${vcf}.gz", emit: gz_vcf

    script:
    """
    bgzip -c $vcf > ${vcf}.gz
    """
}


process INDEX {

    input:
    path vcf

    output:
    path "${vcf}.csi", emit: index

    script:
    """
    bcftools index $vcf
    """
}


process MERGE {

    input: 
        path vcfs
        path indexes
        path rename_chr_map

    output:
        tuple path("multisample.vcf.gz"), path("multisample.vcf.gz.csi")

    script:
    """
    bcftools merge -m none --force-single ${vcfs.join(' ')} | \
        bcftools norm -m - | \
        bcftools annotate --rename-chr ${rename_chr_map} | \
        bcftools sort -W -Oz -o multisample.vcf.gz
    """
}


process SPLIT_CHR {

    publishDir params.prepped_vcfs

    input:
        val chr 
        tuple path(vcf), path(index)

    output:
        path "chr${chr}.vcf.gz"

    script:
    """
    bcftools view -r chr${chr} -Oz -o chr${chr}.vcf.gz $vcf
    """
}

process FIX_CHR_X{

    publishDir params.prepped_vcfs

    input:
        tuple path(vcf), path(index)
        path ploidy_file

    output:
        path "chrX.vcf.gz"

    script:
    """
    bcftools view -r chrX ${vcf} |\
        bcftools +fixploidy -Oz -o chrX.vcf.gz  --  -p ${ploidy_file}
    """
}
