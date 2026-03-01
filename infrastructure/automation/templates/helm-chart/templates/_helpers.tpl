{{/*
Expand the name of the chart.
*/}}
{{- define "ghost.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "ghost.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "ghost.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "ghost.labels" -}}
helm.sh/chart: {{ include "ghost.chart" . }}
{{ include "ghost.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
environment: {{ .Values.global.environment }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "ghost.selectorLabels" -}}
app.kubernetes.io/name: {{ include "ghost.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "ghost.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "ghost.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
MySQL hostname
*/}}
{{- define "ghost.mysql.host" -}}
{{- if .Values.mysql.enabled }}
{{- printf "%s-mysql" .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- .Values.ghostConfig.database.connection.host }}
{{- end }}
{{- end }}

{{/*
Redis hostname
*/}}
{{- define "ghost.redis.host" -}}
{{- if .Values.redis.enabled }}
{{- printf "%s-redis-master" .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- .Values.ghostConfig.redis.host }}
{{- end }}
{{- end }}

{{/*
Ghost URL
*/}}
{{- define "ghost.url" -}}
{{- if .Values.ghostConfig.url }}
{{- .Values.ghostConfig.url }}
{{- else }}
{{- printf "https://%s" .Values.global.domain }}
{{- end }}
{{- end }}

{{/*
Persistence volume claim name
*/}}
{{- define "ghost.pvc.name" -}}
{{- printf "%s-content" (include "ghost.fullname" .) }}
{{- end }}
