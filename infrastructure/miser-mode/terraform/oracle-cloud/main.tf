module "cloud_init" {
  source = "../modules/cloud-init"
}

# Get availability domains
data "oci_identity_availability_domains" "ads" {
  compartment_id = local.compartment_ocid
}

# Get the latest Ubuntu 22.04 ARM64 image
data "oci_core_images" "ubuntu_images" {
  compartment_id           = local.compartment_ocid
  operating_system         = "Canonical Ubuntu"
  operating_system_version = "22.04"
  shape                    = var.instance_shape
  sort_by                  = "TIMECREATED"
  sort_order               = "DESC"
}

locals {
  compartment_ocid = var.compartment_ocid != "" ? var.compartment_ocid : var.tenancy_ocid
  availability_domain = var.availability_domain != "" ? var.availability_domain : data.oci_identity_availability_domains.ads.availability_domains[0].name
  ssh_public_key = var.ssh_public_key != "" ? var.ssh_public_key : file(var.ssh_public_key_path)
}

# Virtual Cloud Network
resource "oci_core_vcn" "ghost_platform_vcn" {
  compartment_id = local.compartment_ocid
  cidr_blocks    = ["10.0.0.0/16"]
  display_name   = "${var.instance_name}-vcn"
  dns_label      = "ghostvcn"
}

# Internet Gateway
resource "oci_core_internet_gateway" "ghost_platform_ig" {
  compartment_id = local.compartment_ocid
  vcn_id         = oci_core_vcn.ghost_platform_vcn.id
  display_name   = "${var.instance_name}-ig"
  enabled        = true
}

# Route Table
resource "oci_core_route_table" "ghost_platform_rt" {
  compartment_id = local.compartment_ocid
  vcn_id         = oci_core_vcn.ghost_platform_vcn.id
  display_name   = "${var.instance_name}-rt"

  route_rules {
    network_entity_id = oci_core_internet_gateway.ghost_platform_ig.id
    destination       = "0.0.0.0/0"
    destination_type  = "CIDR_BLOCK"
  }
}

# Security List
resource "oci_core_security_list" "ghost_platform_sl" {
  compartment_id = local.compartment_ocid
  vcn_id         = oci_core_vcn.ghost_platform_vcn.id
  display_name   = "${var.instance_name}-sl"

  # Egress rules - allow all outbound
  egress_security_rules {
    destination = "0.0.0.0/0"
    protocol    = "all"
  }

  # Ingress rules
  # SSH
  ingress_security_rules {
    protocol = "6"  # TCP
    source   = "0.0.0.0/0"
    tcp_options {
      min = 22
      max = 22
    }
  }

  # HTTP
  ingress_security_rules {
    protocol = "6"
    source   = "0.0.0.0/0"
    tcp_options {
      min = 80
      max = 80
    }
  }

  # HTTPS
  ingress_security_rules {
    protocol = "6"
    source   = "0.0.0.0/0"
    tcp_options {
      min = 443
      max = 443
    }
  }

  # Port 8080
  ingress_security_rules {
    protocol = "6"
    source   = "0.0.0.0/0"
    tcp_options {
      min = 8080
      max = 8080
    }
  }

  # Port 9090
  ingress_security_rules {
    protocol = "6"
    source   = "0.0.0.0/0"
    tcp_options {
      min = 9090
      max = 9090
    }
  }

  # Port 3000
  ingress_security_rules {
    protocol = "6"
    source   = "0.0.0.0/0"
    tcp_options {
      min = 3000
      max = 3000
    }
  }

  # ICMP for ping
  ingress_security_rules {
    protocol = "1"  # ICMP
    source   = "0.0.0.0/0"
  }
}

# Subnet
resource "oci_core_subnet" "ghost_platform_subnet" {
  compartment_id    = local.compartment_ocid
  vcn_id            = oci_core_vcn.ghost_platform_vcn.id
  cidr_block        = "10.0.1.0/24"
  display_name      = "${var.instance_name}-subnet"
  dns_label         = "ghostsubnet"
  route_table_id    = oci_core_route_table.ghost_platform_rt.id
  security_list_ids = [oci_core_security_list.ghost_platform_sl.id]
}

# Compute Instance
resource "oci_core_instance" "ghost_platform" {
  compartment_id      = local.compartment_ocid
  availability_domain = local.availability_domain
  display_name        = var.instance_name
  shape               = var.instance_shape

  shape_config {
    ocpus         = var.instance_ocpus
    memory_in_gbs = var.instance_memory_in_gbs
  }

  source_details {
    source_type             = "image"
    source_id               = data.oci_core_images.ubuntu_images.images[0].id
    boot_volume_size_in_gbs = var.boot_volume_size_in_gbs
  }

  create_vnic_details {
    subnet_id        = oci_core_subnet.ghost_platform_subnet.id
    assign_public_ip = true
    display_name     = "${var.instance_name}-vnic"
  }

  metadata = {
    ssh_authorized_keys = local.ssh_public_key
    user_data           = base64encode(module.cloud_init.user_data)
  }

  freeform_tags = {
    Environment = "miser-mode"
    Platform    = "ghost"
    ManagedBy   = "terraform"
  }
}
