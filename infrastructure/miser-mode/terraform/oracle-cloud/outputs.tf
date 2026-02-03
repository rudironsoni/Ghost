output "server_id" {
  description = "The OCID of the compute instance"
  value       = oci_core_instance.ghost_platform.id
}

output "server_ip" {
  description = "The public IP address of the instance"
  value       = oci_core_instance.ghost_platform.public_ip
}

output "private_ip" {
  description = "The private IP address of the instance"
  value       = oci_core_instance.ghost_platform.private_ip
}

output "ssh_connection_string" {
  description = "SSH connection string for the instance"
  value       = "ssh ubuntu@${oci_core_instance.ghost_platform.public_ip}"
}

output "created_at" {
  description = "Timestamp when the instance was created"
  value       = oci_core_instance.ghost_platform.time_created
}

output "server_name" {
  description = "Name of the instance"
  value       = oci_core_instance.ghost_platform.display_name
}

output "instance_shape" {
  description = "Shape of the instance"
  value       = oci_core_instance.ghost_platform.shape
}

output "availability_domain" {
  description = "Availability domain of the instance"
  value       = oci_core_instance.ghost_platform.availability_domain
}

output "region" {
  description = "Region of the instance"
  value       = var.region
}

output "vcn_id" {
  description = "OCID of the Virtual Cloud Network"
  value       = oci_core_vcn.ghost_platform_vcn.id
}

output "subnet_id" {
  description = "OCID of the subnet"
  value       = oci_core_subnet.ghost_platform_subnet.id
}
