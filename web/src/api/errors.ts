type ApiErrorInit = {
  status: number;
  code: string;
  detail?: string;
  traceId?: string;
  retryAfterSeconds?: number;
  fieldErrors?: Record<string, string[]>;
  extensions?: Record<string, unknown>;
};

/** A failed API call, read from the ProblemDetails body (ISKELET §5.12: code, traceId, errors, extra fields). */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly detail?: string;
  readonly traceId?: string;
  readonly retryAfterSeconds?: number;
  readonly fieldErrors: Record<string, string[]>;
  readonly extensions: Record<string, unknown>;

  constructor(init: ApiErrorInit) {
    super(init.detail ?? init.code);
    this.name = 'ApiError';
    this.status = init.status;
    this.code = init.code;
    this.detail = init.detail;
    this.traceId = init.traceId;
    this.retryAfterSeconds = init.retryAfterSeconds;
    this.fieldErrors = init.fieldErrors ?? {};
    this.extensions = init.extensions ?? {};
  }

  static async fromResponse(response: Response): Promise<ApiError> {
    let body: Record<string, unknown> = {};
    try {
      body = (await response.json()) as Record<string, unknown>;
    } catch {
      // Not JSON (proxy error page, empty body): the status alone is reported.
    }
    const retryAfter = Number(response.headers.get('Retry-After'));
    const { status: _status, code, detail, traceId, errors, title: _title, type: _type, instance: _instance, ...extensions } = body;
    return new ApiError({
      status: response.status,
      code: typeof code === 'string' ? code : `http.${response.status}`,
      detail: typeof detail === 'string' ? detail : undefined,
      traceId: typeof traceId === 'string' ? traceId : undefined,
      retryAfterSeconds: Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : undefined,
      fieldErrors: (errors as Record<string, string[]> | undefined) ?? {},
      extensions,
    });
  }
}

const SESSION_ENDED = 'Oturumunuz sona erdi, lütfen tekrar giriş yapın.';

const messages: Record<string, string> = {
  'validation.failed': 'Lütfen işaretli alanları düzeltin.',
  'auth.invalid_credentials': 'E-posta veya parola hatalı.',
  'auth.email_taken': 'Bu e-posta ile zaten bir hesap var.',
  'auth.refresh_token_invalid': SESSION_ENDED,
  'auth.refresh_token_expired': SESSION_ENDED,
  'auth.refresh_token_reused': SESSION_ENDED,
  'auth.unauthorized': SESSION_ENDED,
  'auth.forbidden': 'Bu işlem için yetkiniz yok.',
  'user.cannot_assign_admin': 'Admin rolü bu ekrandan verilemez.',
  'resource.not_found': 'Aradığınız kayıt bulunamadı.',
  'event.not_owner': 'Bu etkinlik size ait değil.',
  'event.invalid_state': 'Etkinlik bu işlem için uygun durumda değil.',
  'event.has_sold_tickets': 'Satılmış bileti olan etkinlik silinemez.',
  'event.not_on_sale': 'Bu etkinlik şu an satışta değil.',
  'event.missing_section_price': 'Salondaki her bölüm için fiyat girin.',
  'seat.not_in_event': 'Seçilen koltuk bu etkinliğe ait değil.',
  'seat.unavailable': 'Seçtiğiniz koltuklardan biri az önce başkası tarafından alındı.',
  'reservation.already_pending': 'Bu etkinlik için bekleyen bir rezervasyonunuz var.',
  'reservation.expired': 'Rezervasyon süresi doldu; koltuklar serbest bırakıldı.',
  'reservation.invalid_state': 'Rezervasyon bu işlem için uygun durumda değil.',
  'payment.declined': 'Ödeme reddedildi; koltuklar serbest bırakıldı.',
  'payment.idempotency_key_reused': 'Bu ödeme isteği başka bir rezervasyonda kullanılmış.',
  'concurrency.conflict': 'Kayıt aynı anda değişti, lütfen sayfayı yenileyip tekrar deneyin.',
};

/** Turkish message for an API failure (design §8); unknown codes fall back to a generic message with the trace id. */
export function messageFor(error: unknown): string {
  if (!(error instanceof ApiError)) return "Sunucuya ulaşılamadı. Bağlantınızı ve API'nin çalıştığını kontrol edin.";
  if (error.code === 'rate_limit.exceeded') {
    return error.retryAfterSeconds ? `Çok fazla deneme. ${error.retryAfterSeconds} sn sonra tekrar deneyin.` : 'Çok fazla deneme. Biraz sonra tekrar deneyin.';
  }
  return messages[error.code] ?? `Beklenmeyen bir hata oluştu (iz: ${error.traceId ?? 'yok'}).`;
}

/** First API message for a form field, matched by its JSON name (e.g. "password", "sections[0].name"). */
export function fieldError(error: unknown, field: string): string | undefined {
  return error instanceof ApiError ? error.fieldErrors[field]?.[0] : undefined;
}
