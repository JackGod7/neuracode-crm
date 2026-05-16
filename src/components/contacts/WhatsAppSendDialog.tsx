"use client";

import { useState } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { toast } from "sonner";
import { Send } from "lucide-react";

const TEMPLATES = [
  {
    name: "crm_bienvenida",
    label: "Bienvenida",
    category: "Utility",
    language: "es_MX",
    params: [
      { label: "Nombre del contacto", placeholder: "Juan" },
      { label: "Empresa", placeholder: "Neuracode" },
      { label: "Número de seguimiento", placeholder: "1042" },
    ],
  },
  {
    name: "crm_seguimiento",
    label: "Seguimiento de caso",
    category: "Utility",
    language: "es_MX",
    params: [
      { label: "Nombre del contacto", placeholder: "Juan" },
      { label: "Número de caso", placeholder: "#1042" },
      { label: "Empresa", placeholder: "Neuracode" },
      { label: "Fecha del caso", placeholder: "10/05/2026" },
    ],
  },
  {
    name: "crm_propuesta_lista",
    label: "Propuesta lista",
    category: "Marketing",
    language: "es_MX",
    params: [
      { label: "Nombre del contacto", placeholder: "Juan" },
      { label: "Empresa", placeholder: "Neuracode" },
      { label: "Descripción", placeholder: "Implementación CRM" },
      { label: "Inversión", placeholder: "S/ 2,500" },
      { label: "Válida hasta", placeholder: "20/05/2026" },
    ],
  },
  {
    name: "crm_recordatorio_cita",
    label: "Recordatorio de cita",
    category: "Utility",
    language: "es_MX",
    params: [
      { label: "Empresa", placeholder: "Neuracode" },
      { label: "Fecha", placeholder: "12/05/2026" },
      { label: "Hora", placeholder: "10:00 AM" },
      { label: "Modalidad", placeholder: "Videollamada (Google Meet)" },
      { label: "Empresa (pie de página)", placeholder: "Neuracode" },
    ],
  },
];

interface WhatsAppSendDialogProps {
  open: boolean;
  onClose: () => void;
  contactId: string;
  contactName: string;
}

export function WhatsAppSendDialog({ open, onClose, contactId, contactName }: WhatsAppSendDialogProps) {
  const [selectedTemplate, setSelectedTemplate] = useState("");
  const [params, setParams] = useState<string[]>([]);
  const [sending, setSending] = useState(false);

  const template = TEMPLATES.find((t) => t.name === selectedTemplate);

  const handleTemplateChange = (name: string | null) => {
    if (!name) return;
    setSelectedTemplate(name);
    const t = TEMPLATES.find((t) => t.name === name);
    setParams(t ? new Array(t.params.length).fill("") : []);
  };

  const handleSend = async () => {
    if (!template) return;
    setSending(true);
    try {
      const res = await fetch(`/api/contacts/${contactId}/whatsapp/send`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          templateName: template.name,
          languageCode: template.language,
          parameters: params,
        }),
      });
      if (!res.ok) {
        const data = await res.json();
        throw new Error(data.error || "Error al enviar");
      }
      toast.success(`Mensaje enviado a ${contactName}`);
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Error al enviar");
    } finally {
      setSending(false);
    }
  };

  const canSend = !!template && params.every((p) => p.trim().length > 0);

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Enviar WhatsApp — {contactName}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label>Plantilla</Label>
            <Select value={selectedTemplate} onValueChange={handleTemplateChange}>
              <SelectTrigger>
                <SelectValue placeholder="Selecciona una plantilla" />
              </SelectTrigger>
              <SelectContent>
                {TEMPLATES.map((t) => (
                  <SelectItem key={t.name} value={t.name}>
                    <div className="flex items-center gap-2">
                      <span>{t.label}</span>
                      <Badge variant="outline" className="text-xs py-0">{t.category}</Badge>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {template && (
            <div className="space-y-3">
              {template.params.map((p, i) => (
                <div key={i} className="space-y-1.5">
                  <Label className="text-xs text-muted-foreground">
                    {`{{${i + 1}}}`} · {p.label}
                  </Label>
                  <Input
                    value={params[i] ?? ""}
                    onChange={(e) => {
                      const next = [...params];
                      next[i] = e.target.value;
                      setParams(next);
                    }}
                    placeholder={p.placeholder}
                  />
                </div>
              ))}
            </div>
          )}

          <div className="flex justify-end gap-2 pt-2">
            <Button variant="outline" onClick={onClose} disabled={sending}>
              Cancelar
            </Button>
            <Button
              onClick={handleSend}
              disabled={!canSend || sending}
              className="bg-green-600 hover:bg-green-700 text-white"
            >
              <Send className="h-4 w-4 mr-1.5" />
              {sending ? "Enviando..." : "Enviar"}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
