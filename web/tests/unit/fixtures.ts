import type { PageDto, SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { PageCleanup, PageFilter, SessionState } from '$lib/hub/generated/Yass.Core.Domain';

export function page(Index: number, over: Partial<PageDto> = {}): PageDto {
	return {
		Index,
		Rotation: 0,
		Deleted: false,
		IsBlank: false,
		Filter: PageFilter.Original,
		StartsDocument: false,
		Hash: `h${Index}`,
		ThumbnailReady: true,
		Cleanup: PageCleanup.None,
		Skew: 0,
		...over
	};
}

export function session(Id = 's', over: Partial<SessionDto> = {}): SessionDto {
	return {
		Id,
		CreatedAt: '2026-09-24T10:00:00Z',
		State: SessionState.Review,
		Pages: [],
		OptionsUsed: {},
		Events: [],
		SizeBytes: 0,
		...over
	};
}

export function sessionOf(Id: string, n: number, over: Partial<SessionDto> = {}): SessionDto {
	return session(Id, { Pages: Array.from({ length: n }, (_, i) => page(i)), ...over });
}
