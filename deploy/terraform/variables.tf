variable "subscription_id" {
  description = "Azure subscription to deploy into"
  type        = string
}

variable "project" {
  description = "Short name used in every resource name"
  type        = string
  default     = "safar"

  validation {
    condition     = can(regex("^[a-z0-9]{3,12}$", var.project))
    error_message = "Use 3-12 lowercase letters or digits (it becomes part of the registry name)."
  }
}

variable "environment" {
  description = "dev, staging or prod"
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region (Qatar Central is closest to Doha)"
  type        = string
  default     = "qatarcentral"
}

variable "kubernetes_version" {
  description = "AKS version; null = the region's current default"
  type        = string
  default     = null
}

variable "node_vm_size" {
  description = "VM size of the AKS node pool"
  type        = string
  default     = "Standard_B2s"
}

variable "node_min_count" {
  type    = number
  default = 1
}

variable "node_max_count" {
  type    = number
  default = 3
}

variable "postgres_sku" {
  description = "Flexible Server SKU; burstable B1ms is enough for a demo"
  type        = string
  default     = "B_Standard_B1ms"
}

variable "postgres_storage_mb" {
  type    = number
  default = 32768
}

variable "tags" {
  type    = map(string)
  default = {}
}
