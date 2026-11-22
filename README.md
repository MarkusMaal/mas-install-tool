# Markuse asjade juurutamise tööriist

See programm võimaldab automaatselt juurutada seadme Markuse arvuti asjadega juhul kui arvutisse on sisestatud autoriseeritud Markuse mälupulk seade.

Hetkel on funktsionaalsust testitud ainult Linuxis (CachyOS), Windowsi testimine toimub tulevikus.

## Kompileerimine

Selleks, et programmi edukalt kompileerida on vaja lisada puuduolevad .npg arhiivifailid Resources kausta (kohandatud failiformaat, laiendus PNG-le, implementeeritud [NpgTool](NpgTool.cs) klassis):

- bin.npg - Markuse asjad binraarid vastavalt operatsioonisüsteemile
- core.npg - Markuse asjade põhifailid (mas-corefiles ilma alakataloogideta) vastavale operatsioonisüsteemile, lisaks verifile.kf Verifile 2.1 turvakoodiga
- graphics.npg - erinevad ikoonid ja logod
- maia.npg - M.A.I.A. veebiserver (alamkataloogides olevad failid on lisatud juurkataloogi vastava prefiksiga)
- scripts.npg - skriptid Linuxi jaoks
- version.npg - taustapildid ja versiooniinfo (version.ini)
- windows.npg - Windowsi jaoks mõeldud failid, mis paigaldatakse Markuse asjade juurkataloogi

Kui on vajadus näha milliseid konkreetseid faile see programm otsib, saate vaadata [InstallTools.RunSetup](InstallTools.cs) meetodi implementatsiooni.

Seejärel on võimalik programm kompileerida järgmise käsklusega:

`dotnet publish -c Release -o out/ -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugSymbols=false`

## NpgTool kasutamine

See on programmi sisseehitatud töörist, mis võimaldab faile lisada .npg arhiivi ja samuti neid lahti pakkida. Seda kasutatakse ka juurutamise käigus failide lahti pakkimiseks.

Seda saab kasutada käivitades juurutamise tööriista käsurealt järgmiste argumentidega:

Pakkimine: 
`mas-install-tool --deflate-image <tee kaustani>`

Lahti pakkimine:
`mas-install-tool --inflate-image <npg faili asukoht> <sihtkaust>`

Implementatsiooni saab näha [NpgTool](NpgTool.cs) klassist.

## Bless kasutamine

Bless on sisseehitatud tööriist mälupulga autoriseerimiseks. Selle kasutamiseks on vajalik seada, millesse on juba paigaldatud Markuse arvuti asjad.

Kasutamiseks käivita juurutamise töörist administraatori/superkasutaja käsurealt järgmiste argumentidega:

USB seadmete seerianumbrite kuvamine: `mas-install-tool --collect-info`

Mälupulga autoriseerimine: `mas-install-tool --bless <mälupulga juurkaust> <seerianumber, mis vastab mälupulgale>`

Kontrollimine (ainult Linuxis): `mas-install-tool --bless-check` (väljastab õnnestumisel `Bless OK`, vastasel korral `Bless FAIL`)