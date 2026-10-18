import type { Area } from '../types';

export const home = {
	scan: { en: 'Scan', de: 'Scannen', fr: 'Numériser', es: 'Escanear' },
	scanningEllipsis: {
		en: 'Scanning…',
		de: 'Scannt…',
		fr: 'Numérisation…',
		es: 'Escaneando…'
	},
	scanner: { en: 'Scanner', de: 'Scanner', fr: 'Scanner', es: 'Escáner' },
	deviceNotFound: {
		en: '{device} (not found)',
		de: '{device} (nicht gefunden)',
		fr: '{device} (introuvable)',
		es: '{device} (no encontrado)'
	},
	blockedConnecting: {
		en: 'Connecting to the scanner service…',
		de: 'Verbinde mit dem Scan-Dienst…',
		fr: 'Connexion au service de numérisation…',
		es: 'Conectando con el servicio de escaneo…'
	},
	blockedOffline: {
		en: 'Not connected to the scanner service',
		de: 'Keine Verbindung zum Scan-Dienst',
		fr: 'Pas connecté au service de numérisation',
		es: 'Sin conexión con el servicio de escaneo'
	},
	blockedBusy: {
		en: 'The scanner is busy',
		de: 'Der Scanner ist beschäftigt',
		fr: 'Le scanner est occupé',
		es: 'El escáner está ocupado'
	},
	blockedNoDevice: {
		en: 'No scanner found',
		de: 'Kein Scanner gefunden',
		fr: 'Aucun scanner trouvé',
		es: 'No se encontró ningún escáner'
	},
	blockedError: {
		en: 'The scanner reported a problem',
		de: 'Der Scanner meldet ein Problem',
		fr: 'Le scanner signale un problème',
		es: 'El escáner informa de un problema'
	},
	noDeviceHint: {
		en: 'Make sure it is switched on and connected: by USB to the machine running YASS, or on the same network. If it is still not found, that machine may need its driver: a SANE backend on Linux, the maker’s WIA driver on Windows.',
		de: 'Prüfe, ob er eingeschaltet und verbunden ist: per USB mit dem Rechner, auf dem YASS läuft, oder im selben Netzwerk. Wird er dann noch nicht gefunden, braucht dieser Rechner vielleicht seinen Treiber: ein SANE-Backend unter Linux, den WIA-Treiber des Herstellers unter Windows.',
		fr: 'Vérifiez qu’il est allumé et connecté : en USB à la machine qui exécute YASS, ou sur le même réseau. S’il reste introuvable, cette machine a peut-être besoin de son pilote : un backend SANE sous Linux, le pilote WIA du fabricant sous Windows.',
		es: 'Comprueba que esté encendido y conectado: por USB al equipo que ejecuta YASS o en la misma red. Si sigue sin aparecer, puede que ese equipo necesite su controlador: un backend de SANE en Linux, el controlador WIA del fabricante en Windows.'
	},
	errorHint: {
		en: 'Check the scanner itself (paper, cover, cable). It is checked again on its own.',
		de: 'Prüfe den Scanner selbst (Papier, Deckel, Kabel). Er wird von selbst erneut geprüft.',
		fr: 'Vérifiez le scanner lui-même (papier, capot, câble). Il est revérifié automatiquement.',
		es: 'Revisa el propio escáner (papel, tapa, cable). Se vuelve a comprobar solo.'
	},
	offlineHint: {
		en: 'It reconnects on its own as soon as the service can be reached.',
		de: 'Die Verbindung wird von selbst wiederhergestellt, sobald der Dienst erreichbar ist.',
		fr: 'La connexion se rétablit d’elle-même dès que le service est joignable.',
		es: 'Se vuelve a conectar sola en cuanto el servicio esté disponible.'
	},
	blockedChecking: {
		en: 'Still checking the scanner…',
		de: 'Scanner wird noch geprüft…',
		fr: 'Vérification du scanner en cours…',
		es: 'Aún comprobando el escáner…'
	},
	dpi: { en: '{n} dpi', de: '{n} dpi', fr: '{n} dpi', es: '{n} ppp' },
	changeSettings: {
		en: 'Change settings',
		de: 'Einstellungen ändern',
		fr: 'Modifier les réglages',
		es: 'Cambiar ajustes'
	},

	preset: { en: 'Preset', de: 'Profil', fr: 'Profil', es: 'Perfil' },
	saveAsPreset: {
		en: 'Save as preset',
		de: 'Als Profil speichern',
		fr: 'Enregistrer comme profil',
		es: 'Guardar como perfil'
	},
	saveAsPresetHint: {
		en: 'These settings match no preset; save them as one',
		de: 'Diese Einstellungen passen zu keinem Profil; speichere sie als eins',
		fr: 'Ces réglages ne correspondent à aucun profil ; enregistre-les comme profil',
		es: 'Estos ajustes no coinciden con ningún perfil; guárdalos como uno'
	},
	presetEdited: {
		en: 'Edited — press to go back to it',
		de: 'Geändert — drücken, um zurückzukehren',
		fr: 'Modifié — appuie pour y revenir',
		es: 'Editado — pulsa para volver a él'
	},

	autoDeskew: {
		en: 'Auto-deskew',
		de: 'Auto-Begradigen',
		fr: 'Redressement auto',
		es: 'Enderezado auto'
	},
	skipBlankPages: {
		en: 'Skip blank pages',
		de: 'Leere Seiten überspringen',
		fr: 'Ignorer les pages vides',
		es: 'Omitir páginas en blanco'
	},
	tone: {
		en: 'B {b} / C {c}',
		de: 'H {b} / K {c}',
		fr: 'L {b} / C {c}',
		es: 'B {b} / C {c}'
	},

	connecting: { en: 'Connecting…', de: 'Verbinde…', fr: 'Connexion…', es: 'Conectando…' },
	reconnecting: {
		en: 'Reconnecting…',
		de: 'Verbinde neu…',
		fr: 'Reconnexion…',
		es: 'Reconectando…'
	},
	connected: { en: 'Connected', de: 'Verbunden', fr: 'Connecté', es: 'Conectado' },
	offline: { en: 'Offline', de: 'Offline', fr: 'Hors ligne', es: 'Sin conexión' },
	scanningPages: {
		en: { one: 'Scanning · {n} page', other: 'Scanning · {n} pages' },
		de: { one: 'Scannt · {n} Seite', other: 'Scannt · {n} Seiten' },
		fr: { one: 'Numérisation · {n} page', other: 'Numérisation · {n} pages' },
		es: { one: 'Escaneando · {n} página', other: 'Escaneando · {n} páginas' }
	},
	scannerDetails: {
		en: 'Scanner details',
		de: 'Scanner-Details',
		fr: 'Détails du scanner',
		es: 'Detalles del escáner'
	},
	closeScannerDetails: {
		en: 'Close scanner details',
		de: 'Scanner-Details schließen',
		fr: 'Fermer les détails du scanner',
		es: 'Cerrar detalles del escáner'
	},
	detailServer: { en: 'Server', de: 'Server', fr: 'Serveur', es: 'Servidor' },
	detailState: { en: 'State', de: 'Status', fr: 'État', es: 'Estado' },
	detailPagesSoFar: {
		en: 'Pages so far',
		de: 'Seiten bisher',
		fr: "Pages jusqu'ici",
		es: 'Páginas hasta ahora'
	},
	detailDetails: { en: 'Details', de: 'Details', fr: 'Détails', es: 'Detalles' },
	detailLastChecked: {
		en: 'Last checked',
		de: 'Zuletzt geprüft',
		fr: 'Dernière vérification',
		es: 'Última comprobación'
	},

	stateScanning: { en: 'Scanning', de: 'Scannt', fr: 'Numérisation', es: 'Escaneando' },
	stateReview: {
		en: 'Ready to review',
		de: 'Bereit zur Prüfung',
		fr: 'Prêt à vérifier',
		es: 'Listo para revisar'
	},
	statePreparing: { en: 'Preparing', de: 'Wird vorbereitet', fr: 'Préparation', es: 'Preparando' },
	stateFailed: { en: 'Failed', de: 'Fehlgeschlagen', fr: 'Échec', es: 'Error' },
	stateIdle: { en: 'Idle', de: 'Bereit', fr: 'Inactif', es: 'Inactivo' },
	scannerReady: { en: 'Ready', de: 'Bereit', fr: 'Prêt', es: 'Listo' },
	scannerNoDevice: { en: 'No scanner', de: 'Kein Scanner', fr: 'Aucun scanner', es: 'Sin escáner' },
	scannerError: {
		en: 'Scanner error',
		de: 'Scannerfehler',
		fr: 'Erreur du scanner',
		es: 'Error del escáner'
	},
	scannerChecking: { en: 'Checking…', de: 'Prüfe…', fr: 'Vérification…', es: 'Comprobando…' },

	sourceFeeder: { en: 'Feeder', de: 'Einzug', fr: 'Chargeur', es: 'Alimentador' },
	sourceDuplex: {
		en: 'Feeder, both sides',
		de: 'Einzug, beidseitig',
		fr: 'Chargeur, recto verso',
		es: 'Alimentador, doble cara'
	},
	sourceFlatbed: { en: 'Flatbed', de: 'Flachbett', fr: 'Vitre', es: 'Cristal' },
	colorBw: { en: 'Black & white', de: 'Schwarzweiß', fr: 'Noir et blanc', es: 'Blanco y negro' },
	colorGrayscale: {
		en: 'Grayscale',
		de: 'Graustufen',
		fr: 'Niveaux de gris',
		es: 'Escala de grises'
	},
	colorColor: { en: 'Color', de: 'Farbe', fr: 'Couleur', es: 'Color' },
	sizeLetter: { en: 'Letter', de: 'Letter', fr: 'Lettre US', es: 'Carta' },
	sizeLegal: { en: 'Legal', de: 'Legal', fr: 'Légal US', es: 'Oficio' },

	statusDraft: { en: 'Draft', de: 'Entwurf', fr: 'Brouillon', es: 'Borrador' },
	statusExported: {
		en: 'On server',
		de: 'Auf dem Server',
		fr: 'Sur le serveur',
		es: 'En el servidor'
	},
	statusSaved: { en: 'Saved', de: 'Gespeichert', fr: 'Enregistré', es: 'Guardado' },
	statusDownloaded: { en: 'Saved', de: 'Gespeichert', fr: 'Enregistré', es: 'Guardado' },
	statusDiscarded: { en: 'Discarded', de: 'Verworfen', fr: 'Abandonné', es: 'Descartado' },

	pastScans: {
		en: 'Past scans',
		de: 'Bisherige Scans',
		fr: 'Scans précédents',
		es: 'Escaneos anteriores'
	},
	view: { en: 'View', de: 'Ansicht', fr: 'Affichage', es: 'Vista' },
	gridView: {
		en: 'Grid view',
		de: 'Rasteransicht',
		fr: 'Vue en grille',
		es: 'Vista de cuadrícula'
	},
	listView: { en: 'List view', de: 'Listenansicht', fr: 'Vue en liste', es: 'Vista de lista' },
	searchPastScans: {
		en: 'Search past scans',
		de: 'Bisherige Scans durchsuchen',
		fr: 'Rechercher dans les scans précédents',
		es: 'Buscar en escaneos anteriores'
	},
	searchScans: {
		en: 'Search scans',
		de: 'Scans durchsuchen',
		fr: 'Rechercher',
		es: 'Buscar escaneos'
	},
	select: { en: 'Select', de: 'Auswählen', fr: 'Sélectionner', es: 'Seleccionar' },
	all: { en: 'All', de: 'Alle', fr: 'Tous', es: 'Todos' },
	emptyTitle: {
		en: 'No past scans yet',
		de: 'Noch keine Scans',
		fr: "Aucun scan pour l'instant",
		es: 'Aún no hay escaneos'
	},
	emptyBody: {
		en: 'Every scan is saved here automatically.',
		de: 'Jeder Scan landet automatisch hier.',
		fr: 'Chaque scan est enregistré ici automatiquement.',
		es: 'Cada escaneo se guarda aquí automáticamente.'
	},
	noMatches: {
		en: 'No matches',
		de: 'Keine Treffer',
		fr: 'Aucun résultat',
		es: 'Sin resultados'
	},
	noMatchesBody: {
		en: 'Nothing named “{query}”.',
		de: 'Nichts namens „{query}“.',
		fr: 'Rien ne s’appelle « {query} ».',
		es: 'Nada se llama «{query}».'
	},
	noStatusMatches: {
		en: 'Nothing with this status',
		de: 'Nichts mit diesem Status',
		fr: 'Rien avec ce statut',
		es: 'Nada con este estado'
	},
	noStatusMatchesBody: {
		en: 'Try another status filter.',
		de: 'Probier einen anderen Statusfilter.',
		fr: 'Essaie un autre filtre de statut.',
		es: 'Prueba otro filtro de estado.'
	},
	scanCount: {
		en: { one: '{n} scan', other: '{n} scans' },
		de: { one: '{n} Scan', other: '{n} Scans' },
		fr: { one: '{n} scan', other: '{n} scans' },
		es: { one: '{n} escaneo', other: '{n} escaneos' }
	},
	clearAll: { en: 'Clear all', de: 'Alle löschen', fr: 'Tout effacer', es: 'Borrar todo' },
	confirmClearAll: {
		en: 'Delete every scan?',
		de: 'Alle Scans löschen?',
		fr: 'Supprimer tous les scans ?',
		es: '¿Eliminar todos los escaneos?'
	},
	downloaded: { en: 'Saved', de: 'Gespeichert', fr: 'Enregistré', es: 'Guardado' },
	downloadedFiles: {
		en: { one: 'Saved {n} file', other: 'Saved {n} files' },
		de: { one: '{n} Datei gespeichert', other: '{n} Dateien gespeichert' },
		fr: { one: '{n} fichier enregistré', other: '{n} fichiers enregistrés' },
		es: { one: '{n} archivo guardado', other: '{n} archivos guardados' }
	},
	scansDeleted: {
		en: { one: '{n} scan deleted', other: '{n} scans deleted' },
		de: { one: '{n} Scan gelöscht', other: '{n} Scans gelöscht' },
		fr: { one: '{n} scan supprimé', other: '{n} scans supprimés' },
		es: { one: '{n} escaneo eliminado', other: '{n} escaneos eliminados' }
	},

	pageCount: {
		en: { one: '{n} page', other: '{n} pages' },
		de: { one: '{n} Seite', other: '{n} Seiten' },
		fr: { one: '{n} page', other: '{n} pages' },
		es: { one: '{n} página', other: '{n} páginas' }
	},
	downloadedToBrowser: {
		en: 'Saved to this browser',
		de: 'Über diesen Browser gespeichert',
		fr: 'Enregistré depuis ce navigateur',
		es: 'Guardado desde este navegador'
	},
	notDownloaded: {
		en: 'Not saved yet',
		de: 'Noch nicht gespeichert',
		fr: 'Pas encore enregistré',
		es: 'Aún no guardado'
	},
	downloadNamed: {
		en: 'Save {title}',
		de: '{title} speichern',
		fr: 'Enregistrer {title}',
		es: 'Guardar {title}'
	},
	deleteNamed: {
		en: 'Delete {title}',
		de: '{title} löschen',
		fr: 'Supprimer {title}',
		es: 'Eliminar {title}'
	},
	confirmDelete: { en: 'Delete?', de: 'Löschen?', fr: 'Supprimer ?', es: '¿Eliminar?' },
	confirmDeleteNamed: {
		en: 'Really delete {title}?',
		de: '{title} wirklich löschen?',
		fr: 'Vraiment supprimer {title} ?',
		es: '¿Eliminar {title} de verdad?'
	},

	sortBy: { en: 'Sort by', de: 'Sortieren nach', fr: 'Trier par', es: 'Ordenar por' },
	closeSortMenu: {
		en: 'Close sort menu',
		de: 'Sortiermenü schließen',
		fr: 'Fermer le menu de tri',
		es: 'Cerrar el menú de orden'
	},
	sortNewest: {
		en: 'Newest first',
		de: 'Neueste zuerst',
		fr: 'Plus récents',
		es: 'Más recientes'
	},
	sortOldest: {
		en: 'Oldest first',
		de: 'Älteste zuerst',
		fr: 'Plus anciens',
		es: 'Más antiguos'
	},
	sortName: { en: 'Name A–Z', de: 'Name A–Z', fr: 'Nom A–Z', es: 'Nombre A–Z' },
	sortNewestShort: { en: 'Newest', de: 'Neueste', fr: 'Récents', es: 'Recientes' },
	sortOldestShort: { en: 'Oldest', de: 'Älteste', fr: 'Anciens', es: 'Antiguos' },
	sortNameShort: { en: 'Name', de: 'Name', fr: 'Nom', es: 'Nombre' },

	selectedCount: {
		en: '{n} selected',
		de: '{n} ausgewählt',
		fr: '{n} sélectionné(s)',
		es: '{n} seleccionado(s)'
	},
	selectScans: {
		en: 'Select scans',
		de: 'Scans auswählen',
		fr: 'Sélectionner des scans',
		es: 'Seleccionar escaneos'
	},
	selectAll: {
		en: 'Select all',
		de: 'Alle auswählen',
		fr: 'Tout sélectionner',
		es: 'Seleccionar todo'
	},
	selectNone: {
		en: 'Select none',
		de: 'Keine auswählen',
		fr: 'Tout désélectionner',
		es: 'No seleccionar nada'
	},
	downloadSelected: {
		en: 'Save selected',
		de: 'Auswahl speichern',
		fr: 'Enregistrer la sélection',
		es: 'Guardar selección'
	},
	deleteSelected: {
		en: 'Delete selected',
		de: 'Auswahl löschen',
		fr: 'Supprimer la sélection',
		es: 'Eliminar selección'
	},
	confirmDeleteCount: {
		en: 'Delete {n}?',
		de: '{n} löschen?',
		fr: 'Supprimer {n} ?',
		es: '¿Eliminar {n}?'
	},
	done: { en: 'Done', de: 'Fertig', fr: 'Terminé', es: 'Listo' }
} satisfies Area;
