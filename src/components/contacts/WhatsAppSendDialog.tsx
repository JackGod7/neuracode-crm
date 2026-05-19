"use client";

import { useState, useEffect } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { Textarea } from "@/components/ui/textarea";
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

interface WindowStatus {
  isOpen: boolean;
  secondsRemaining: number | null;
}

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
  const [mode, setMode] = useState<"template" | "freetext">("template");
  const [freeText, setFreeText] = useState("");
  const [windowStatus, setWindowStatus] = useState<WindowStatus | null>(null);

  useEffect(() => {
    if (!open) {
      setFreeText("");
      setMode("template");
      setWindowStatus(null);
      return;
    }
    fetch(`/api/contacts/${contactId}/wa-window`)
      .then((r) => r.json())
      .then((data: { isOpen: boolean; secondsRemaining: number | null }) => {
        setWindowStatus({ isOpen: data.isOpen, secondsRemaining: data.secondsRemaining });
        if (data.isOpen) setMode("freetext");
      })
      .catch(() => setWindowStatus({ isOpen: false, secondsRemaining: null }));
  }, [open, contactId]);

  const template = TEMPLATES.find((t) => t.name === selectedTemplate);

  const handleTemplateChange = (name: string | null) => {
    if (!name) return;
    setSelectedTemplate(name);
    const t = TEMPLATES.find((t) => t.name === name);
    setParams(t ? new Array(t.params.length).fill("") : []);
  };

  const handleSend = async () => {
    setSending(true);
    try {
      const body =
        mode === "freetext"
          ? { type: "text" as const, content: freeText }
          : {
              type: "template" as const,
              templateName: template!.name,
              languageCode: template!.language,
              parameters: params,
            };
      const res = await fetch(`/api/contacts/${contactId}/wa-send`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        const data = await res.json();
        throw new Error((data as { error?: string }).error ?? "Error al enviar");
      }
      toast.success(`Mensaje enviado a ${contactName}`);
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Error al enviar");
    } finally {
      setSending(false);
    }
  };

  const canSend =
    mode === "freetext"
      ? freeText.trim().length > 0
      : !!template && params.every((p) => p.trim().length > 0);

  const hoursRemaining =
    windowStatus?.isOpen && windowStatus.secondsRemaining != null
      ? Math.floor(windowStatus.secondsRemaining / 3600)
      : null;

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Enviar WhatsApp — {contactName}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          {windowStatus?.isOpen && (
            <div className="flex items-center justify-between">
              <div className="flex gap-2">
                <Button
                  size="sm"
                  variant={mode === "freetext" ? "default" : "outline"}
                  onClick={() => setMode("freetext")}
                >
                  Texto libre
                </Button>
                <Button
                  size="sm"
                  variant={mode === "template" ? "default" : "outline"}
                  onClick={() => setMode("template")}
                >
                  Plantilla
                </Button>
              </div>
              {hoursRemaining != null && (
                <span className="text-xs text-green-600 font-medium">
                  Ventana abierta · {hoursRemaining}h restantes
                </span>
              )}
            </div>
          )}

          {mode === "freetext" ? (
            <div className="space-y-1.5">
              <Label>Mensaje</Label>
              <Textarea
                value={freeText}
                onChange={(e) => setFreeText(e.target.value)}
                placeholder="Escribe tu mensaje..."
                rows={4}
              />
            </div>
          ) : (
            <>
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
            </>
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
