# Production Deployment Checklist

## Pre-Deployment

### AWS Account Setup
- [ ] AWS account created and configured
- [ ] IAM user/role with necessary permissions
- [ ] AWS CLI configured (`aws configure`)
- [ ] MFA enabled on AWS account
- [ ] Billing alerts configured
- [ ] Cost budgets set up

### SSL/TLS Certificate
- [ ] Domain name registered
- [ ] ACM certificate requested
- [ ] Domain validation completed
- [ ] Certificate ARN noted

### Secrets Preparation
- [ ] Strong RDS master password generated (min 16 chars)
- [ ] Redis auth token generated (min 16 chars)
- [ ] Amazon MQ admin password generated (min 16 chars)
- [ ] Amazon MQ app password generated (min 16 chars)
- [ ] Secrets stored in password manager
- [ ] AWS Secrets Manager configured (optional)

### Network Planning
- [ ] VPC CIDR decided (default: 10.0.0.0/16)
- [ ] Subnet ranges planned
- [ ] Office/VPN IP ranges identified for ALB access
- [ ] DNS configuration planned

### Tools Installation
- [ ] Terraform >= 1.6.0 installed
- [ ] AWS CLI >= 2.0 installed
- [ ] kubectl >= 1.28 installed
- [ ] Helm >= 3.0 installed (optional)
- [ ] jq installed (for JSON parsing)

### Backend Configuration
- [ ] Backend choice made (Terraform Cloud vs S3)
- [ ] Terraform Cloud account created (if using)
- [ ] S3 bucket + DynamoDB created (if using S3)
- [ ] Backend configuration updated in backend.tf

### Code Review
- [ ] Review main.tf for correctness
- [ ] Review variables.tf for appropriate defaults
- [ ] Review terraform.tfvars for sensitive data
- [ ] Create terraform.tfvars.local with actual values
- [ ] Verify .gitignore includes sensitive files
- [ ] Code peer-reviewed

## Deployment

### Initialize
- [ ] `terraform init` successful
- [ ] Providers downloaded
- [ ] Backend initialized
- [ ] Modules downloaded

### Validation
- [ ] `terraform validate` passes
- [ ] `terraform fmt -check` passes
- [ ] No syntax errors
- [ ] Security scan completed (tfsec/checkov)
- [ ] Cost estimation reviewed (infracost)

### Planning
- [ ] `terraform plan` executed
- [ ] Plan reviewed thoroughly
- [ ] Resource count verified (~100-150 resources)
- [ ] No unexpected changes
- [ ] Plan saved to file
- [ ] Plan reviewed by team lead (if applicable)

### Apply
- [ ] Maintenance window scheduled
- [ ] Stakeholders notified
- [ ] `terraform apply` executed
- [ ] Deployment monitored
- [ ] No errors during apply
- [ ] All resources created successfully
- [ ] Deployment time noted (~25-35 minutes)

## Post-Deployment

### Verification
- [ ] VPC created with correct CIDR
- [ ] Subnets created in all 3 AZs
- [ ] NAT Gateways active (3 total)
- [ ] EKS cluster status: ACTIVE
- [ ] EKS nodes joined cluster (5 nodes)
- [ ] RDS instance status: Available
- [ ] RDS Multi-AZ: Enabled
- [ ] ElastiCache cluster status: Available
- [ ] Amazon MQ broker status: Running
- [ ] ALB status: Active
- [ ] WAF associated with ALB
- [ ] KMS keys created and active

### Access Configuration
- [ ] kubectl configured: `make configure-kubectl`
- [ ] kubectl can connect to cluster
- [ ] Node status verified: `kubectl get nodes`
- [ ] System pods running: `kubectl get pods -n kube-system`

### Database Setup
- [ ] RDS endpoint accessible from EKS
- [ ] Database connection tested
- [ ] Master password stored securely
- [ ] Read replica status verified
- [ ] Automated backups configured
- [ ] Performance Insights enabled

### Cache Configuration
- [ ] Redis connection tested from EKS
- [ ] Auth token working
- [ ] Cluster endpoints noted
- [ ] Replication verified

### Messaging Setup
- [ ] RabbitMQ console accessible
- [ ] Admin credentials working
- [ ] App credentials working
- [ ] Cluster status: Running
- [ ] HA verified (Multi-AZ)

### Network & Security
- [ ] Security groups configured correctly
- [ ] VPC Flow Logs enabled
- [ ] CloudTrail logging verified
- [ ] WAF rules active
- [ ] Rate limiting tested
- [ ] HTTPS redirect working
- [ ] SSL certificate valid

### Monitoring
- [ ] CloudWatch log groups created
- [ ] Logs flowing to CloudWatch
- [ ] SNS topic created for alerts
- [ ] Alert email subscription confirmed
- [ ] CloudWatch dashboards created
- [ ] Container Insights enabled

### DNS Configuration
- [ ] ALB DNS name noted
- [ ] Route53 record created (A or ALIAS)
- [ ] DNS propagation verified
- [ ] Health checks passing

### Application Deployment
- [ ] Ghost application deployed to EKS
- [ ] Ingress configured
- [ ] TLS working
- [ ] Application accessible via domain
- [ ] Database migrations completed
- [ ] Media uploads tested

### Performance Testing
- [ ] Load testing completed
- [ ] Response times acceptable
- [ ] Auto-scaling tested
- [ ] Database performance verified
- [ ] Cache hit rate monitored

### Backup Verification
- [ ] RDS automated backup verified
- [ ] Manual RDS snapshot created
- [ ] ElastiCache snapshot created
- [ ] Terraform state backed up
- [ ] Backup restoration tested (DR drill)

### Documentation
- [ ] Infrastructure diagram created
- [ ] Runbook updated
- [ ] Access credentials documented
- [ ] On-call procedures defined
- [ ] Incident response plan ready

## Cost Optimization

### Reserved Instances
- [ ] Usage patterns analyzed (after 1 week)
- [ ] Reserved Instance purchases planned
- [ ] EKS node RIs purchased (1-year)
- [ ] RDS RIs purchased (1-year)
- [ ] ElastiCache RIs purchased (1-year)
- [ ] Amazon MQ RIs considered

### Cost Monitoring
- [ ] Cost allocation tags verified
- [ ] Cost Explorer dashboard created
- [ ] Budget alerts configured
- [ ] Monthly cost review scheduled
- [ ] Savings plan considered

### Optimization Opportunities
- [ ] Right-sizing analysis completed
- [ ] Unused resources identified
- [ ] Auto-scaling thresholds tuned
- [ ] S3 lifecycle policies configured
- [ ] CloudWatch log retention set

## Security Hardening

### Access Control
- [ ] IAM roles reviewed
- [ ] Least privilege verified
- [ ] Service accounts configured (IRSA)
- [ ] Pod security policies applied
- [ ] Network policies configured

### Secrets Management
- [ ] All passwords rotated
- [ ] Secrets in AWS Secrets Manager
- [ ] Kubernetes secrets configured
- [ ] No secrets in code/config

### Compliance
- [ ] Security audit completed
- [ ] Compliance tags applied
- [ ] Audit logging verified
- [ ] Encryption verified (at rest & transit)
- [ ] GuardDuty enabled
- [ ] Security Hub enabled
- [ ] AWS Config enabled

### Vulnerability Management
- [ ] Container scanning enabled
- [ ] ECR scan on push configured
- [ ] Dependency scanning enabled
- [ ] Security patches applied
- [ ] CVE monitoring configured

## Disaster Recovery

### Backup Testing
- [ ] Database restore tested
- [ ] Cache restore tested
- [ ] Application restore tested
- [ ] Recovery time measured (RTO)
- [ ] Data loss assessed (RPO)

### DR Procedures
- [ ] DR runbook documented
- [ ] Failover procedures tested
- [ ] Cross-region replication configured
- [ ] DR drills scheduled (quarterly)

### High Availability
- [ ] Multi-AZ verified
- [ ] Auto-scaling working
- [ ] Health checks passing
- [ ] Failover tested
- [ ] Zero-downtime deployment tested

## Ongoing Maintenance

### Daily
- [ ] Monitor CloudWatch dashboards
- [ ] Check error logs
- [ ] Verify backup completion
- [ ] Review security alerts

### Weekly
- [ ] Review cost reports
- [ ] Check scaling events
- [ ] Review performance metrics
- [ ] Update documentation

### Monthly
- [ ] Security patch review
- [ ] Access review
- [ ] Capacity planning
- [ ] Cost optimization review
- [ ] Backup restoration test

### Quarterly
- [ ] Kubernetes version upgrade
- [ ] RDS minor version upgrade
- [ ] Security audit
- [ ] DR drill
- [ ] Performance tuning
- [ ] Architecture review

## Sign-off

### Team Approvals
- [ ] Infrastructure team: _______________
- [ ] Security team: _______________
- [ ] DevOps lead: _______________
- [ ] Product owner: _______________

### Production Readiness
- [ ] All critical checklist items completed
- [ ] Known issues documented
- [ ] Support team trained
- [ ] Monitoring confirmed working
- [ ] Runbooks available

### Go-Live
- [ ] Deployment date: _______________
- [ ] Deployment time: _______________
- [ ] Deployed by: _______________
- [ ] Success criteria met: [ ]

---

## Notes

### Known Issues


### Lessons Learned


### Future Improvements


---

**Checklist Version**: 1.0  
**Last Updated**: 2026-02-03  
**Next Review**: _______________
