from __future__ import annotations

import math
import shutil
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

from PIL import Image, ImageDraw, ImageFont
from docx import Document
from docx.shared import Inches, Pt

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
IMAGES = DOCS / "Report_Images"
REPORT = DOCS / "Software_Architecture_Design_Report_AIVES_Group3_EN_remake.docx"
FONT = Path(r"C:\Windows\Fonts\arial.ttf")
FONT_BOLD = Path(r"C:\Windows\Fonts\arialbd.ttf")
W, H = 1800, 1180
INK = (24, 31, 39)
MUTED = (72, 82, 92)
PAPER = (255, 255, 255)
PALE = (246, 248, 250)


def font(size: int, bold: bool = False):
    return ImageFont.truetype(str(FONT_BOLD if bold else FONT), size)


def canvas(title: str, subtitle: str = ""):
    im = Image.new("RGB", (W, H), PAPER)
    d = ImageDraw.Draw(im)
    d.text((W / 2, 34), title, font=font(32, True), fill=INK, anchor="mt")
    if subtitle:
        d.text((W / 2, 80), subtitle, font=font(17), fill=MUTED, anchor="mt")
    d.line((55, 120, W - 55, 120), fill=(175, 182, 189), width=2)
    return im, d


def wrap(draw, text, f, max_width):
    lines = []
    for paragraph in text.split("\n"):
        words, line = paragraph.split(), ""
        for word in words:
            trial = f"{line} {word}".strip()
            if draw.textbbox((0, 0), trial, font=f)[2] <= max_width:
                line = trial
            else:
                if line:
                    lines.append(line)
                line = word
        lines.append(line)
    return lines


def box(d, xy, title, body="", w=220, h=100, r=14, fill=PAPER, title_size=20, body_size=16):
    x, y = xy
    d.rounded_rectangle((x, y, x + w, y + h), radius=r, fill=fill, outline=INK, width=2)
    d.text((x + w / 2, y + 12), title, font=font(title_size, True), fill=INK, anchor="mt")
    if body:
        f = font(body_size)
        lines = wrap(d, body, f, w - 24)
        yy = y + 42
        for line in lines:
            d.text((x + w / 2, yy), line, font=f, fill=INK, anchor="mt")
            yy += body_size + 5


def oval(d, xy, title, body="", w=255, h=86, fs=18):
    x, y = xy
    d.ellipse((x, y, x + w, y + h), fill=PAPER, outline=INK, width=2)
    lines = wrap(d, title + (("\n" + body) if body else ""), font(fs), w - 24)
    lineh = fs + 5
    yy = y + (h - len(lines) * lineh) / 2
    for line in lines:
        d.text((x + w / 2, yy), line, font=font(fs), fill=INK, anchor="mt")
        yy += lineh


def arrow(d, p1, p2, label="", color=INK, width=3, dashed=False, label_offset=(0, -14)):
    x1, y1 = p1; x2, y2 = p2
    if dashed:
        dx, dy = x2 - x1, y2 - y1
        dist = max(1, math.hypot(dx, dy)); ux, uy = dx / dist, dy / dist
        pos = 0
        while pos < dist - 12:
            end = min(pos + 13, dist - 10)
            d.line((x1 + ux * pos, y1 + uy * pos, x1 + ux * end, y1 + uy * end), fill=color, width=width)
            pos += 22
    else:
        d.line((x1, y1, x2, y2), fill=color, width=width)
    ang = math.atan2(y2 - y1, x2 - x1); L = 13
    a = (x2 - L * math.cos(ang - .45), y2 - L * math.sin(ang - .45))
    b = (x2 - L * math.cos(ang + .45), y2 - L * math.sin(ang + .45))
    d.polygon([(x2, y2), a, b], fill=color)
    if label:
        mx, my = (x1 + x2) / 2 + label_offset[0], (y1 + y2) / 2 + label_offset[1]
        f = font(15, True)
        bb = d.textbbox((mx, my), label, font=f, anchor="mm")
        d.rounded_rectangle((bb[0] - 5, bb[1] - 3, bb[2] + 5, bb[3] + 3), radius=4, fill=PAPER)
        d.text((mx, my), label, font=f, fill=INK, anchor="mm")


def save_image(im, name):
    IMAGES.mkdir(parents=True, exist_ok=True)
    p = IMAGES / name
    im.save(p, optimize=True)
    return p


def architecture():
    im,d=canvas("AIVES SYSTEM ARCHITECTURE","Technology stack and request / response paths")
    # Separate columns and wide gutters keep all connector labels clear.
    for rect in [(320,175,720,1080),(850,175,1280,1080),(1410,175,1750,700),(1410,785,1750,1080)]:
        d.rounded_rectangle(rect,radius=24,fill=PAPER,outline=INK,width=3)
    for x,label,y in [(520,"FRONTEND WEB",195),(1065,"BACKEND SERVER",195),(1580,"THIRD-PARTY SERVICES",195),(1580,"DATABASE",805)]:
        d.text((x,y),label,font=font(20 if label!="THIRD-PARTY SERVICES" else 17,True),fill=INK,anchor="mt")
    box(d,(45,490),"Lecturer / Student","Browser client",w=185,h=145,fill=PAPER,title_size=18)
    def logo_tile(x,y,name,sub,kind):
        d.rounded_rectangle((x,y,x+170,y+170),radius=16,fill=PAPER,outline=(110,120,130),width=2)
        cx,cy=x+85,y+65
        if kind=="react":
            d.ellipse((cx-34,cy-13,cx+34,cy+13),outline=(24,155,210),width=3)
            d.ellipse((cx-18,cy-34,cx+18,cy+34),outline=(24,155,210),width=3)
            d.ellipse((cx-4,cy-4,cx+4,cy+4),fill=(24,155,210))
        elif kind=="vite":
            d.polygon([(cx-35,cy-31),(cx+34,cy-31),(cx+5,cy+34)],fill=(105,70,220))
            d.polygon([(cx+4,cy-31),(cx+28,cy-31),(cx-1,cy+17),(cx+7,cy-1),(cx-7,cy)],fill=(250,190,35))
        elif kind=="ant":
            d.polygon([(cx,cy-35),(cx+32,cy-17),(cx+32,cy+17),(cx,cy+35),(cx-32,cy+17),(cx-32,cy-17)],outline=(30,120,200),fill=PAPER,width=4)
            d.ellipse((cx-7,cy-7,cx+7,cy+7),fill=(30,120,200))
        elif kind=="gemini":
            d.polygon([(cx,cy-36),(cx+10,cy-10),(cx+36,cy),(cx+10,cy+10),(cx,cy+36),(cx-10,cy+10),(cx-36,cy),(cx-10,cy-10)],fill=(90,115,235))
        elif kind=="speech":
            d.rounded_rectangle((cx-25,cy-30,cx+25,cy+10),radius=22,outline=INK,width=4)
            d.arc((cx-38,cy-18,cx+38,cy+30),0,180,fill=INK,width=4);d.line((cx,cy+30,cx,cy+44),fill=INK,width=4);d.line((cx-18,cy+44,cx+18,cy+44),fill=INK,width=4)
        elif kind=="sql":
            d.ellipse((cx-30,cy-28,cx+30,cy-8),outline=(30,105,190),fill=(225,240,255),width=3)
            d.rectangle((cx-30,cy-18,cx+30,cy+25),fill=(225,240,255),outline=(30,105,190),width=3)
            d.arc((cx-30,cy+12,cx+30,cy+34),0,180,fill=(30,105,190),width=3)
        elif kind=="dotnet":
            d.ellipse((cx-34,cy-34,cx+34,cy+34),fill=(95,52,155));d.text((cx,cy),".NET",font=font(19,True),fill=PAPER,anchor="mm")
        else:
            d.ellipse((cx-28,cy-28,cx+28,cy+28),outline=INK,width=3);d.text((cx,cy),"HTTP",font=font(12,True),fill=INK,anchor="mm")
        d.text((cx,y+117),name,font=font(16,True),fill=INK,anchor="mt")
        d.text((cx,y+143),sub,font=font(12),fill=MUTED,anchor="mt")
    logo_tile(345,285,"React 19","Web UI","react");logo_tile(535,285,"Vite 8","Build tool","vite")
    logo_tile(345,495,"Ant Design 6","UI library","ant");logo_tile(535,495,"Axios","REST client","http")
    box(d,(365,740),"React Router 6 + Auth Context","Client routing · localStorage",w=310,h=115,fill=PAPER,title_size=17,body_size=14)
    logo_tile(875,285,"ASP.NET Core","Web API · .NET 10","dotnet")
    box(d,(1060,310),"REST API","JWT Bearer auth",w=190,h=120,fill=PAPER,title_size=18,body_size=15)
    box(d,(895,495),"FluentValidation","Request validation",w=160,h=110,fill=PAPER,title_size=16,body_size=14)
    box(d,(1080,495),"Application services","Questions · exams · grading",w=165,h=110,fill=PAPER,title_size=15,body_size=13)
    box(d,(895,665),"SignalR hub","Student follow-up",w=160,h=100,fill=PAPER,title_size=16,body_size=14)
    box(d,(1080,665),"EF Core 10","SQL Server provider",w=165,h=100,fill=PAPER,title_size=16,body_size=13)
    logo_tile(1495,255,"Google Gemini API","AI service","gemini")
    logo_tile(1495,475,"Azure AI Speech","STT · TTS","speech")
    logo_tile(1495,830,"Azure SQL Database","Managed SQL Server","sql")
    d.text((1580,1015),"Target database; live connection not verified",font=font(12),fill=MUTED,anchor="mt")
    # Break the group outlines at the connector gates so the borders do not run through labels.
    for x,spans in [(320,[(525,595)]),(720,[(455,525)]),(850,[(455,525)]),
                    (1280,[(340,410),(550,620),(890,960)]),(1410,[(340,410),(550,620),(890,960)])]:
        for y1,y2 in spans: d.rectangle((x-5,y1,x+5,y2),fill=PAPER)
    # Two separate, one-way arrows document request and response for every integration.
    pairs=[((230,535),(320,535),"Request"),((320,585),(230,585),"Response"),
      ((720,465),(850,465),"Request"),((850,515),(720,515),"Response"),
      ((1280,350),(1495,350),"Request"),((1495,400),(1280,400),"Response"),
      ((1280,560),(1495,560),"Request"),((1495,610),(1280,610),"Response"),
      ((1280,900),(1495,900),"SQL request"),((1495,950),(1280,950),"DB response")]
    for a,b,label in pairs: arrow(d,a,b,label,label_offset=(0,-16 if b[1]<a[1] or b[0]>a[0] else 17),width=2)
    for a,b in [((1060,430),(1060,495)),((1060,605),(1060,665))]:arrow(d,a,b,"",width=2)
    # Protocol details are conveyed in the report text; keep the diagram footer empty.
    return save_image(im,"01_System_Architecture_Current.png")


def usecase():
    im,d=canvas("AIVES USE CASE OVERVIEW","Current lecturer, student, and external-service interactions")
    d.rounded_rectangle((285,155,1535,1120),radius=12,outline=INK,width=3,fill=PAPER)
    d.text((910,168),"AI-Powered Viva Examination System (AIVES)",font=font(21,True),fill=INK,anchor="mt")
    def actor(x,y,label):
        d.ellipse((x-13,y,x+13,y+26),outline=INK,width=2);d.line((x,y+26,x,y+72),fill=INK,width=2)
        d.line((x-27,y+46,x+27,y+46),fill=INK,width=2);d.line((x,y+72,x-23,y+104),fill=INK,width=2);d.line((x,y+72,x+23,y+104),fill=INK,width=2)
        d.text((x,y+112),label,font=font(17,True),fill=INK,anchor="mt")
    actor(125,385,"Lecturer");actor(125,930,"Student");actor(1680,690,"AI / Speech APIs")
    lecturer=[("Create or update questions",250),("Manage course rubric",345),("Generate question draft",440),
              ("Create or manage exam session",535),("Review and finalize assessment",630)]
    student=[("Start exam and answer questions",780),("Request AI follow-up",850),("Submit voice answer",920),
             ("Submit attempt",990),("View finalized result",1060)]
    for label,y in lecturer+student: oval(d,(500,y),label,w=400,h=58,fs=15)
    # Actors connect to a shared vertical bus; short branches avoid the starburst of direct links.
    d.line((410,270,410,688),fill=INK,width=2);d.line((410,810,410,1090),fill=INK,width=2)
    d.line((152,431,410,431),fill=INK,width=2);d.line((152,976,410,976),fill=INK,width=2)
    for _,y in lecturer+student:
        cy=y+29; d.line((410,cy,500,cy),fill=INK,width=2)
    # Third-party calls associate only with capabilities backed by AI/Speech providers.
    external_y=[440+29,850+29,920+29,630+29]
    for cy in external_y: d.rectangle((1530,cy-5,1540,cy+5),fill=PAPER)
    d.line((1580,external_y[0],1580,external_y[-1]),fill=INK,width=2)
    d.line((1560,external_y[0],1580,external_y[0]),fill=INK,width=2)
    d.line((900,external_y[0],1560,external_y[0]),fill=INK,width=2)
    for cy in external_y[1:-1]: d.line((900,cy,1580,cy),fill=INK,width=2)
    d.line((900,external_y[-1],1580,external_y[-1]),fill=INK,width=2)
    d.line((1580,external_y[0],1580,700),fill=INK,width=2)
    d.line((1580,700,1650,700),fill=INK,width=2)
    d.text((910,1157),"External providers support AI generation, interview follow-up, voice transcription, and grading.",font=font(13),fill=MUTED,anchor="mt")
    return save_image(im,"02_UseCase_Current.png")


def erd():
    im, d = canvas("AIVES CONCEPTUAL ERD", "Business entities and relationships; keys and database types intentionally omitted")
    nodes={
        "User":(60,430),"Course":(450,175),"Question":(890,175),
        "Rubric":(60,175),"ExamSession":(450,430),"SessionQuestion":(890,430),
        "Transcript":(60,690),"TranscriptTurn":(450,690),"Assessment":(890,690)}
    desc={"User":"Lecturer or student account","Course":"Subject and shared rubric owner","Question":"Question-bank item","Rubric":"Course scoring criterion",
          "ExamSession":"Scheduled viva session","SessionQuestion":"Ordered question and rubric snapshot","Transcript":"One student attempt",
          "TranscriptTurn":"Ordered AI/student conversation turn","Assessment":"AI suggestion and lecturer final score"}
    for name,(x,y) in nodes.items(): box(d,(x,y),name,desc[name],w=300,h=110,fill=PALE,title_size=19,body_size=15)
    links=[("User","ExamSession","lecturer creates 0..* sessions"),("Course","Question","course contains 0..* questions"),
           ("Course","Rubric","course defines 0..* rubrics"),("Course","ExamSession","course has 0..* sessions"),
           ("ExamSession","SessionQuestion","session has 1..* snapshots"),("Question","SessionQuestion","question selected in 0..* snapshots"),
           ("User","Transcript","student has 0..* attempts"),("ExamSession","Transcript","session records 0..* attempts"),
           ("Transcript","TranscriptTurn","attempt contains 0..* turns"),("Transcript","Assessment","attempt has 0..* assessments"),
           ("SessionQuestion","Assessment","snapshot has 0..1 assessment per attempt"),("User","Assessment","lecturer finalizes 0..* assessments")]
    # Draw relationship paths before entity cards so lines never obscure entity text.
    for a,b,label in links:
        ax,ay=nodes[a]; bx,by=nodes[b]
        if ay==by:
            p1=(ax+300,ay+55);p2=(bx,by+55);d.line((p1,p2),fill=(90,100,110),width=2);tx,ty=(p1[0]+p2[0])//2,p1[1]-17
        elif ax==bx:
            p1=(ax+150,ay+110 if by>ay else ay);p2=(bx+150,by if by>ay else by+110);d.line((p1,p2),fill=(90,100,110),width=2);tx,ty=p1[0]+72,(p1[1]+p2[1])//2
        else:
            p1=(ax+300,ay+55) if bx>ax else (ax+150,ay+110 if by>ay else ay);p2=(bx,by+55) if bx>ax else (bx+150,by if by>ay else by+110)
            mx=(p1[0]+p2[0])//2;d.line((p1[0],p1[1],mx,p1[1],mx,p2[1],p2[0],p2[1]),fill=(90,100,110),width=2);tx,ty=mx,(p1[1]+p2[1])//2
        # Relationship text is placed in a separate key below so it never collides with entities.
    for name,(x,y) in nodes.items(): box(d,(x,y),name,desc[name],w=300,h=110,fill=PALE,title_size=19,body_size=15)
    d.text((65,850),"RELATIONSHIPS & CARDINALITIES",font=font(18,True),fill=INK)
    rels=["Course 1 → 0..* Questions","Course 1 → 0..* Rubrics","Course 1 → 0..* ExamSessions",
          "ExamSession 1 → 1..* SessionQuestions","Question 1 → 0..* SessionQuestions","ExamSession 1 → 0..* Transcripts",
          "User (lecturer) 1 → 0..* ExamSessions","User (student) 1 → 0..* Transcripts","Transcript 1 → 0..* TranscriptTurns",
          "Transcript 1 → 0..* Assessments","SessionQuestion 1 → 0..1 Assessment per attempt","User (lecturer) 1 → 0..* finalized Assessments"]
    for i,text in enumerate(rels):
        x=65 if i<6 else 920; y=890+(i%6)*42
        d.text((x,y),"• "+text,font=font(14),fill=INK)
    return save_image(im,"03_Conceptual_ERD_Current.png")


def class_diagram():
    im,d=canvas("AIVES DOMAIN CLASS MODEL", "Core implementation classes and relationships; methods omitted for readability")
    data={
      "User":(60,430,["Role","FullName","IsDeleted"]),"Course":(450,175,["Code","Name","IsDeleted"]),"Question":(890,175,["Content","BloomLevel","CourseId"]),
      "Rubric":(60,175,["Criteria","Weight","MaxScore","CourseId"]),
      "ExamSession":(450,430,["SessionTitle","StartTime / EndTime","Status"]),"ExamSessionQuestion":(890,430,["DisplayOrder","Question snapshot","RubricSnapshotJson"]),
      "Transcript":(60,690,["Status","StartedAt / SubmittedAt","TotalScore?"]),"TranscriptTurn":(450,690,["Sequence","Speaker","ContentText","Timestamp"]),
      "Assessment":(890,690,["AiSuggestedScore?","FinalScore?","TeacherComment?","Version"])}
    boxes={}
    for name,(x,y,fields) in data.items(): boxes[name]=(x,y,285,90+len(fields)*25)
    assoc=[("Course","Question","1 to 0..*"),("Course","Rubric","1 to 0..*"),("Course","ExamSession","1 to 0..*"),
           ("ExamSession","ExamSessionQuestion","1 to 1..*"),("Question","ExamSessionQuestion","1 to 0..*"),
           ("ExamSession","Transcript","1 to 0..*"),("User","Transcript","1 to 0..*"),
           ("Transcript","TranscriptTurn","1 to 0..*"),("Transcript","Assessment","1 to 0..*"),
           ("ExamSessionQuestion","Assessment","1 to 0..1 per attempt")]
    for a,b,label in assoc:
        x,y,w,h=boxes[a]; X,Y,Wb,Hb=boxes[b]
        if Y==y:
            p1=(x+w,y+h//2);p2=(X,Y+Hb//2);d.line((p1,p2),fill=(90,100,110),width=2);tx,ty=(p1[0]+p2[0])//2,p1[1]-14
        elif X==x:
            p1=(x+w//2,y+h);p2=(X+Wb//2,Y);d.line((p1,p2),fill=(90,100,110),width=2);tx,ty=p1[0]+75,(p1[1]+p2[1])//2
        else:
            p1=(x+w,y+h//2);p2=(X+Wb//2,Y);mx=(p1[0]+p2[0])//2;d.line((p1[0],p1[1],mx,p1[1],mx,p2[1],p2[0],p2[1]),fill=(90,100,110),width=2);tx,ty=mx,(p1[1]+p2[1])//2
        # Association labels are listed below the class boxes to prevent edge/text overlap.
    for name,(x,y,fields) in data.items():
        w=285;h=90+len(fields)*25;d.rectangle((x,y,x+w,y+h),outline=INK,width=2,fill=PAPER);d.rectangle((x,y,x+w,y+38),outline=INK,width=2,fill=PALE)
        d.text((x+w/2,y+8),name,font=font(19,True),fill=INK,anchor="mt")
        for i,fld in enumerate(fields):d.text((x+12,y+49+i*25),fld,font=font(15),fill=INK)
    d.text((60,965),"ASSOCIATIONS",font=font(17,True),fill=INK)
    relation_lines=["Course 1 — 0..* Question","Course 1 — 0..* Rubric","Course 1 — 0..* ExamSession",
      "ExamSession 1 — 1..* ExamSessionQuestion","Question 1 — 0..* ExamSessionQuestion",
      "ExamSession 1 — 0..* Transcript","User 1 — 0..* Transcript","Transcript 1 — 0..* TranscriptTurn",
      "Transcript 1 — 0..* Assessment","ExamSessionQuestion 1 — 0..1 Assessment per attempt"]
    for i,text in enumerate(relation_lines):
        x=60 if i<5 else 880; y=1005+(i%5)*29
        d.text((x,y),"• "+text,font=font(13),fill=INK)
    return save_image(im,"04_Class_Diagram_Current.png")


def sequence(title, subtitle, participants, steps, filename):
    # Dynamic height prevents the final messages from being clipped at the page edge.
    global H
    old_h=H
    H=max(1100, 290+len(steps)*66+70)
    im,d=canvas(title,subtitle)
    n=len(participants); left=115; right=W-115; gap=(right-left)/(n-1)
    xs=[int(left+i*gap) for i in range(n)]
    for x,name in zip(xs,participants):
        d.rounded_rectangle((x-90,145,x+90,205),radius=6,outline=INK,width=2,fill=PALE)
        head_lines=wrap(d,name,font(16,True),160)
        d.multiline_text((x,175),"\n".join(head_lines),font=font(16,True),fill=INK,anchor="mm",align="center",spacing=1)
        for y in range(215,H-45,18): d.line((x,y,x,min(y+9,H-45)),fill=(115,125,135),width=2)
    yy=250
    for i,(src,dst,label,reply) in enumerate(steps,1):
        x1,x2=xs[src],xs[dst]
        if src==dst:
            d.line((x1,yy,x1+50,yy,x1+50,yy+25,x1,yy+25),fill=INK,width=2)
            d.polygon([(x1,yy+25),(x1+11,yy+19),(x1+11,yy+31)],fill=INK)
            if label:
                d.text((x1+58,yy+11),label,font=font(12,True),fill=INK,anchor="lm")
        else:
            arrow(d,(x1,yy),(x2,yy),"", width=2, dashed=reply)
            maxw=max(155,min(abs(x2-x1)-26,520))
            lines=wrap(d,label,font(18,True),maxw)
            msg="\n".join(lines)
            bb=d.multiline_textbbox(((x1+x2)/2,(yy-22)),msg,font=font(18,True),anchor="mm",align="center",spacing=1)
            d.rounded_rectangle((bb[0]-5,bb[1]-2,bb[2]+5,bb[3]+2),radius=3,fill=PAPER)
            d.multiline_text(((x1+x2)/2,(yy-22)),msg,font=font(18,True),fill=INK,anchor="mm",align="center",spacing=1)
        yy+=66
    result=save_image(im,filename)
    H=old_h
    return result


def state_machine():
    im,d=canvas("AIVES STATE MACHINES", "Persisted session and attempt states; Flow 1 request steps are not Question status values")
    # Sessions row
    d.text((80,155),"ExamSession",font=font(22,True),fill=INK)
    states=[("Draft",170), ("Open",500), ("Closed",830)]
    for label,x in states: box(d,(x,205),label,w=210,h=76,fill=PALE)
    box(d,(1220,365),"Cancelled",w=210,h=76,fill=PALE)
    arrow(d,(380,243),(500,243),"open")
    arrow(d,(710,243),(830,243),"close")
    # Cancellation paths use separate lower rails and enter the state from above.
    d.line((275,281,275,320,1300,320,1300,365),fill=INK,width=2)
    d.polygon([(1300,365),(1293,352),(1307,352)],fill=INK)
    d.text((760,303),"cancel",font=font(14,True),fill=INK,anchor="mm",stroke_width=3,stroke_fill=PAPER)
    d.line((605,281,605,340,1360,340,1360,365),fill=INK,width=2)
    d.polygon([(1360,365),(1353,352),(1367,352)],fill=INK)
    d.text((970,342),"cancel",font=font(14,True),fill=INK,anchor="mm",stroke_width=3,stroke_fill=PAPER)
    # attempts row
    d.text((80,465),"Transcript / student attempt",font=font(22,True),fill=INK)
    xs=[70,405,740,1075,1410]; labels=["InProgress","Submitted","Grading","AwaitingReview","Finalized"]
    for x,l in zip(xs,labels): box(d,(x,535),l,w=220,h=78,fill=PALE,title_size=16)
    for i,label in enumerate(["submit","grade","AI done","all final"]): arrow(d,(xs[i]+220,574),(xs[i+1],574),label,label_offset=(0,-15))
    arrow(d,(850,613),(515,680),"failure / retry",label_offset=(0,18))
    d.rounded_rectangle((115,760,1685,895),radius=12,outline=(100,110,120),width=2,fill=(250,251,252))
    d.text((145,785),"Flow 1 boundary",font=font(18,True),fill=INK)
    d.text((145,822),"Question create/update validation and soft deletion are request workflow steps. Course rubrics are managed separately.",font=font(16),fill=INK)
    return save_image(im,"05_State_Machines_Current.png")


def emit_drawio(path: Path, title: str, nodes: list[dict], edges: list[dict], width=1500, height=940):
    mx = ET.Element("mxfile", {"host":"app.diagrams.net", "modified":"2026-11-01T00:00:00.000Z", "agent":"Codex", "version":"24.7.17", "type":"device"})
    dg=ET.SubElement(mx,"diagram",{"id":"page1","name":title})
    model=ET.SubElement(dg,"mxGraphModel",{"dx":"1500","dy":"940","grid":"1","gridSize":"10","guides":"1","tooltips":"1","connect":"1","arrows":"1","fold":"1","page":"1","pageScale":"1","pageWidth":str(width),"pageHeight":str(height),"math":"0","shadow":"0"})
    root=ET.SubElement(model,"root");ET.SubElement(root,"mxCell",{"id":"0"});ET.SubElement(root,"mxCell",{"id":"1","parent":"0"})
    for i,n in enumerate(nodes):
        style=n.get("style","rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;fontColor=#111111;fontFamily=Arial;fontSize=16;")
        c=ET.SubElement(root,"mxCell",{"id":n["id"],"value":n["label"],"style":style,"vertex":"1","parent":"1"})
        ET.SubElement(c,"mxGeometry",{"x":str(n.get("x",80)),"y":str(n.get("y",150)),"width":str(n.get("w",220)),"height":str(n.get("h",90)),"as":"geometry"})
    for i,e in enumerate(edges):
        style=e.get("style","edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;strokeColor=#222222;endArrow=classic;endFill=1;fontSize=13;fontFamily=Arial;")
        c=ET.SubElement(root,"mxCell",{"id":e.get("id",f"e{i}"),"value":e.get("label",""),"style":style,"edge":"1","parent":"1","source":e["source"],"target":e["target"]})
        ET.SubElement(c,"mxGeometry",{"relative":"1","as":"geometry"})
    path.write_text(ET.tostring(mx,encoding="unicode"),encoding="utf-8")


def emit_sequence(path: Path, title: str, participants: list[str], steps: list[tuple[int,int,str,bool]], width=1800, height=None):
    height=max(1100,290+len(steps)*60+70) if height is None else height
    mx=ET.Element("mxfile",{"host":"app.diagrams.net","modified":"2026-11-01T00:00:00.000Z","agent":"Codex","version":"24.7.17","type":"device"})
    dg=ET.SubElement(mx,"diagram",{"id":"sequence","name":title})
    model=ET.SubElement(dg,"mxGraphModel",{"dx":str(width),"dy":str(height),"grid":"1","gridSize":"10","page":"1","pageScale":"1","pageWidth":str(width),"pageHeight":str(height)})
    root=ET.SubElement(model,"root"); ET.SubElement(root,"mxCell",{"id":"0"}); ET.SubElement(root,"mxCell",{"id":"1","parent":"0"})
    left,right=80,width-80; gap=(right-left)/(len(participants)-1)
    xs=[int(left+i*gap) for i in range(len(participants))]
    def vertex(cid,label,x,y,w,h,style):
        c=ET.SubElement(root,"mxCell",{"id":cid,"value":label,"style":style,"vertex":"1","parent":"1"})
        ET.SubElement(c,"mxGeometry",{"x":str(x),"y":str(y),"width":str(w),"height":str(h),"as":"geometry"})
    def edge(cid,source,target,label,style):
        c=ET.SubElement(root,"mxCell",{"id":cid,"value":label,"style":style,"edge":"1","parent":"1","source":source,"target":target})
        ET.SubElement(c,"mxGeometry",{"relative":"1","as":"geometry"})
    for i,(name,x) in enumerate(zip(participants,xs)):
        vertex(f"p{i}",name,x-95,135,190,60,"rounded=0;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;fontColor=#111111;fontFamily=Arial;fontSize=16;fontStyle=1;align=center;verticalAlign=middle;")
        vertex(f"l{i}a","",x-2,200,4,4,"ellipse;fillOpacity=0;strokeOpacity=0;opacity=0;")
        vertex(f"l{i}b","",x-2,height-55,4,4,"ellipse;fillOpacity=0;strokeOpacity=0;opacity=0;")
        edge(f"life{i}",f"l{i}a",f"l{i}b","","endArrow=none;startArrow=none;dashed=1;dashPattern=6 5;strokeColor=#777777;strokeWidth=1;html=1;")
    y=245
    for i,(src,dst,label,reply) in enumerate(steps):
        x1,x2=xs[src],xs[dst]
        aid=f"a{i}"; bid=f"b{i}"
        vertex(aid,"",x1-2,y-2,4,4,"ellipse;fillOpacity=0;strokeOpacity=0;opacity=0;")
        vertex(bid,"",x2-2,y-2,4,4,"ellipse;fillOpacity=0;strokeOpacity=0;opacity=0;")
        style="endArrow=block;endFill=1;html=1;strokeColor=#222222;fontFamily=Arial;fontSize=15;labelBackgroundColor=#FFFFFF;"
        if reply: style+="dashed=1;dashPattern=6 4;"
        edge(f"msg{i}",aid,bid,label,style)
        y+=60
    path.write_text(ET.tostring(mx,encoding="unicode"),encoding="utf-8")


def install_usecase_source():
    nodes=[
      {"id":"boundary","label":"","x":285,"y":155,"w":1250,"h":965,"style":"rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=#222222;strokeWidth=2;"},
      {"id":"system-title","label":"AI-Powered Viva Examination System (AIVES)","x":650,"y":160,"w":520,"h":40,"style":"text;html=1;align=center;verticalAlign=middle;fontStyle=1;fontSize=20;fontFamily=Arial;"},
    ]
    actor_style="shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;outlineConnect=0;fontFamily=Arial;fontSize=15;strokeColor=#222222;fillColor=#FFFFFF;"
    for i,(name,x,y) in enumerate([("Lecturer",70,350),("Student",70,900),("AI / Speech APIs",1630,635)]):
        nodes.append({"id":f"actor{i}","label":name,"x":x,"y":y,"w":90,"h":125,"style":actor_style})
    lecturer=[("question","Create or update questions",250),("rubric","Manage course rubric",345),("generate","Generate question draft",440),
              ("session","Create or manage exam session",535),("grading","Review and finalize assessment",630)]
    student=[("attempt","Start exam and answer questions",780),("followup","Request AI follow-up",850),("voice","Submit voice answer",920),
             ("submit","Submit attempt",990),("result","View finalized result",1060)]
    for uid,label,y in lecturer+student:
        nodes.append({"id":uid,"label":label,"x":500,"y":y,"w":400,"h":58,"style":"ellipse;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;fontColor=#111111;fontFamily=Arial;fontSize=15;"})
    # Bus lines and branch anchors make role associations parallel and non-crossing.
    nodes += [
      {"id":"lecturer-bus","label":"","x":410,"y":270,"w":2,"h":420,"style":"rounded=0;fillColor=#222222;strokeColor=#222222;"},
      {"id":"student-bus","label":"","x":410,"y":810,"w":2,"h":280,"style":"rounded=0;fillColor=#222222;strokeColor=#222222;"},
      {"id":"provider-bus","label":"","x":1575,"y":455,"w":2,"h":500,"style":"rounded=0;fillColor=#222222;strokeColor=#222222;"},
    ]
    edges=[]
    simple="edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#222222;endArrow=none;startArrow=none;"
    edges += [{"source":"actor0","target":"lecturer-bus","style":simple},{"source":"actor1","target":"student-bus","style":simple},{"source":"actor2","target":"provider-bus","style":simple}]
    for uid,_,y in lecturer:
        ratio=(y+29-270)/420
        edges.append({"source":"lecturer-bus","target":uid,"style":simple+f"exitX=1;exitY={ratio};entryX=0;entryY=0.5;"})
    for uid,_,y in student:
        ratio=(y+29-810)/280
        edges.append({"source":"student-bus","target":uid,"style":simple+f"exitX=1;exitY={ratio};entryX=0;entryY=0.5;"})
    external_rows={"generate":440+29,"followup":850+29,"voice":920+29,"grading":630+29}
    for uid,cy in external_rows.items():
        ratio=(cy-455)/500
        edges.append({"source":uid,"target":"provider-bus","style":simple+f"exitX=1;exitY=0.5;entryX=0;entryY={ratio};"})
    emit_drawio(DOCS/"UseCase-remake.drawio","AIVES use case overview (remake)",nodes,edges,width=1800,height=1180)


def install_class_source():
    classes=[("User",60,150,["Role: string","FullName: string","IsDeleted: bool"]),("Course",430,150,["Code: string","Name: string","IsDeleted: bool"]),
      ("Question",800,150,["Content: string","BloomLevel: enum","CourseId: Guid"]),("Rubric",1170,150,["Criteria: string","Weight: decimal","MaxScore: decimal","CourseId: Guid"]),
      ("ExamSession",60,480,["SessionTitle: string","StartTime / EndTime","Status: ExamSessionStatus"]),("ExamSessionQuestion",430,480,["DisplayOrder: int","Content / Points snapshot","RubricSnapshotJson"]),
      ("Transcript",800,480,["Status: TranscriptStatus","StartedAt / SubmittedAt","TotalScore: decimal?"]),("TranscriptTurn",1170,480,["Sequence: int","Speaker / ContentText","Timestamp: DateTime"]),
      ("Assessment",610,760,["AiSuggestedScore: decimal?","FinalScore: decimal?","TeacherComment? / Version"])]
    nodes=[]
    for idx,(name,x,y,fields) in enumerate(classes):
        val=f"<b>{name}</b><hr>{'<br>'.join(fields)}"
        nodes.append({"id":f"c{idx}","label":val,"x":x,"y":y,"w":285,"h":120+20*len(fields),"style":"swimlane;html=1;startSize=34;horizontal=0;rounded=0;fillColor=#FFFFFF;swimlaneFillColor=#F5F6F7;strokeColor=#222222;fontColor=#111111;fontFamily=Arial;fontSize=14;"})
    edges=[]
    relation=[("c1","c2","1 to 0..*"),("c1","c3","1 to 0..*"),("c1","c4","1 to 0..*"),("c4","c5","1 to 1..*"),
      ("c2","c5","1 to 0..*"),("c4","c6","1 to 0..*"),("c0","c6","1 to 0..*"),("c6","c7","1 to 0..*"),
      ("c6","c8","1 to 0..*"),("c5","c8","1 to 0..1 per attempt")]
    for a,b,l in relation: edges.append({"source":a,"target":b,"label":l,"style":"edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#222222;endArrow=none;fontFamily=Arial;fontSize=11;labelBackgroundColor=#FFFFFF;"})
    emit_drawio(DOCS/"AIVES-UML-Class-Diagram-remake.drawio","AIVES core domain class model (remake)",nodes,edges)


def install_drawio_sources():
    # Clean, editable, monochrome diagrams. The report images are rendered from matching content.
    erd_nodes=[(i,n,x,y) for i,n,x,y in [
      ("user","USER<br>Lecturer / student",60,190),("course","COURSE<br>Shared question bank and rubric owner",465,190),("question","QUESTION<br>Viva question",870,190),
      ("rubric","RUBRIC<br>Course scoring criterion",60,440),("session","EXAM SESSION<br>Scheduled viva",465,440),("sq","SESSION QUESTION<br>Question and rubric snapshot",870,440),
      ("transcript","TRANSCRIPT<br>One student attempt",60,690),("turn","TRANSCRIPT TURN<br>Ordered conversation entry",465,690),("assessment","ASSESSMENT<br>AI suggestion and lecturer score",870,690)]]
    nodes=[{"id":i,"label":n,"x":x,"y":y,"w":300,"h":110,"style":"rounded=0;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;fontColor=#111111;fontFamily=Arial;fontSize=16;"} for i,n,x,y in erd_nodes]
    edges=[{"source":a,"target":b,"label":l,"style":"edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#222222;startArrow=ERone;endArrow=ERmany;fontFamily=Arial;fontSize=12;labelBackgroundColor=#FFFFFF;"} for a,b,l in [
      ("user","session","creates"),("course","question","contains"),("course","rubric","defines"),("course","session","hosts"),
      ("session","sq","contains"),("question","sq","selected in"),("user","transcript","student submits"),("session","transcript","records"),
      ("transcript","turn","contains"),("transcript","assessment","has"),("sq","assessment","scored in attempt"),("user","assessment","lecturer finalizes")]]
    emit_drawio(DOCS/"Conceptual-ERD-AIVES-remake.drawio","Conceptual ERD - AIVES (remake)",nodes,edges)
    state_nodes=[{"id":x,"label":lab,"x":xx,"y":yy,"w":220,"h":78} for x,lab,xx,yy in [
      ("sd","Draft",170,205),("so","Open",500,205),("sc","Closed",830,205),("sx","Cancelled",1220,365),
      ("ti","InProgress",70,535),("ts","Submitted",405,535),("tg","Grading",740,535),("tr","AwaitingReview",1075,535),("tf","Finalized",1410,535)]]
    state_edges=[
      {"source":"sd","target":"so","label":"open"},{"source":"so","target":"sc","label":"close"},
      {"source":"sd","target":"sx","label":"cancel","style":"edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#222222;endArrow=block;endFill=1;fontFamily=Arial;fontSize=12;labelBackgroundColor=#FFFFFF;exitX=0.5;exitY=1;entryX=0.3;entryY=0;"},
      {"source":"so","target":"sx","label":"cancel","style":"edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#222222;endArrow=block;endFill=1;fontFamily=Arial;fontSize=12;labelBackgroundColor=#FFFFFF;exitX=0.5;exitY=1;entryX=0.7;entryY=0;"},
      {"source":"ti","target":"ts","label":"submit"},{"source":"ts","target":"tg","label":"grade"},
      {"source":"tg","target":"tr","label":"AI done"},{"source":"tg","target":"ts","label":"failure / retry"},{"source":"tr","target":"tf","label":"all final"}]
    emit_drawio(DOCS/"State-Machine-Exam-Lifecycle-remake.drawio","Exam and grading lifecycle (remake)",state_nodes,state_edges)
    arch_nodes=[
      {"id":"browser","label":"Lecturer / Student<br>Web browser","x":45,"y":475,"w":230,"h":150},
      {"id":"fe-zone","label":"Frontend Web","x":400,"y":210,"w":420,"h":790,"style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;strokeWidth=2;fontFamily=Arial;fontStyle=1;fontSize=20;verticalAlign=top;spacingTop=14;"},
      {"id":"react-icon","label":"","x":455,"y":300,"w":86,"h":86,"style":"shape=image;html=1;aspect=fixed;image=https://icons.diagrams.net/assets/carbon-icons/1/Logo_React.svg;"},
      {"id":"react-label","label":"React 19<br>Vite 8","x":430,"y":390,"w":140,"h":50,"style":"text;html=1;align=center;verticalAlign=middle;fontFamily=Arial;fontSize=16;fontStyle=1;"},
      {"id":"vite-icon","label":"","x":645,"y":300,"w":86,"h":86,"style":"shape=image;html=1;aspect=fixed;image=https://icons.diagrams.net/assets/tabler-outline/1/Brand_Vite.svg;"},
      {"id":"vite-label","label":"Vite build","x":620,"y":390,"w":140,"h":50,"style":"text;html=1;align=center;verticalAlign=middle;fontFamily=Arial;fontSize=16;fontStyle=1;"},
      {"id":"ant-icon","label":"","x":455,"y":510,"w":80,"h":80,"style":"shape=hexagon;html=1;fillColor=#FFFFFF;strokeColor=#222222;strokeWidth=2;"},
      {"id":"ant-label","label":"Ant Design 6<br>UI components","x":430,"y":595,"w":150,"h":55,"style":"text;html=1;align=center;verticalAlign=middle;fontFamily=Arial;fontSize=15;"},
      {"id":"axios-icon","label":"HTTP","x":645,"y":510,"w":86,"h":80,"style":"ellipse;html=1;whiteSpace=wrap;fillColor=#FFFFFF;strokeColor=#222222;strokeWidth=2;fontFamily=Arial;fontSize=13;fontStyle=1;"},
      {"id":"axios-label","label":"Axios REST client","x":615,"y":595,"w":150,"h":55,"style":"text;html=1;align=center;verticalAlign=middle;fontFamily=Arial;fontSize=15;"},
      {"id":"fe-auth","label":"React Router · Auth Context<br>localStorage","x":455,"y":745,"w":310,"h":100,"style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;fontFamily=Arial;fontSize=15;"},
      {"id":"be-zone","label":"Backend · ASP.NET Core / .NET 10","x":910,"y":210,"w":420,"h":790,"style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;strokeWidth=2;fontFamily=Arial;fontStyle=1;fontSize=20;verticalAlign=top;spacingTop=14;"},
      {"id":"net-icon","label":"","x":970,"y":300,"w":100,"h":85,"style":"shape=image;html=1;aspect=fixed;image=https://icons.diagrams.net/assets/developer/1/NET_Core.svg;"},
      {"id":"api-label","label":"ASP.NET Core Web API<br>REST · JWT Bearer","x":1080,"y":300,"w":210,"h":85,"style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;fontFamily=Arial;fontSize=16;fontStyle=1;"},
      {"id":"validation","label":"FluentValidation","x":950,"y":465,"w":160,"h":85,"style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;fontFamily=Arial;fontSize=15;"},
      {"id":"security","label":"JWT Bearer<br>Role authorization","x":1130,"y":465,"w":160,"h":85,"style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;fontFamily=Arial;fontSize=15;"},
      {"id":"data-layer","label":"Entity Framework Core 10<br>SQL Server provider","x":950,"y":620,"w":340,"h":100,"style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;fontFamily=Arial;fontSize=16;"},
      {"id":"signalr","label":"SignalR hub<br>Student follow-up","x":950,"y":790,"w":340,"h":95,"style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;fontFamily=Arial;fontSize=15;"},
      {"id":"third-zone","label":"Third-Party Services","x":1470,"y":210,"w":285,"h":440,"style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;strokeWidth=2;fontFamily=Arial;fontStyle=1;fontSize=18;verticalAlign=top;spacingTop=14;"},
      {"id":"gemini-icon","label":"","x":1575,"y":285,"w":75,"h":75,"style":"shape=image;html=1;aspect=fixed;image=https://icons.diagrams.net/assets/font-awesome/1/Gemini.svg;"},
      {"id":"gemini-label","label":"Google Gemini API","x":1500,"y":365,"w":230,"h":48,"style":"text;html=1;align=center;verticalAlign=middle;fontFamily=Arial;fontSize=16;fontStyle=1;"},
      {"id":"gemini-use","label":"Question generation<br>Interview follow-up<br>AI grading","x":1505,"y":410,"w":220,"h":95,"style":"text;html=1;align=center;verticalAlign=middle;fontFamily=Arial;fontSize=14;"},
      {"id":"speech-icon","label":"","x":1580,"y":520,"w":64,"h":64,"style":"image;aspect=fixed;html=1;image=https://app.diagrams.net/img/lib/azure2/ai_machine_learning/Speech_Services.svg;"},
      {"id":"speech-label","label":"Azure AI Speech<br>STT · TTS","x":1490,"y":580,"w":245,"h":58,"style":"text;html=1;align=center;verticalAlign=middle;fontFamily=Arial;fontSize=15;fontStyle=1;"},
      {"id":"db-zone","label":"Database","x":1470,"y":755,"w":285,"h":245,"style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#222222;strokeWidth=2;fontFamily=Arial;fontStyle=1;fontSize=18;verticalAlign=top;spacingTop=14;"},
      {"id":"sql-icon","label":"","x":1575,"y":815,"w":72,"h":78,"style":"image;aspect=fixed;html=1;image=https://icons.diagrams.net/assets/azure/1/SQL_Database.svg;"},
      {"id":"sql-label","label":"Azure SQL Database","x":1490,"y":900,"w":245,"h":48,"style":"text;html=1;align=center;verticalAlign=middle;fontFamily=Arial;fontSize=16;fontStyle=1;"},
      {"id":"tech-note","label":"Separate Request / Response lines · REST/HTTPS + JSON/JWT · SignalR · Gemini/Speech HTTPS · SQL over TLS<br>Provider credentials and Azure SQL live connection require environment verification","x":900,"y":1030,"w":850,"h":65,"style":"text;html=1;align=center;verticalAlign=middle;fontFamily=Arial;fontSize=13;"},
    ]
    def pair(src,dst,label,revlabel,offset1="0.40",offset2="0.60"):
        return [
          {"source":src,"target":dst,"label":label,"style":f"edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#222222;endArrow=block;endFill=1;fontFamily=Arial;fontSize=12;labelBackgroundColor=#FFFFFF;exitX=1;exitY={offset1};entryX=0;entryY={offset1};"},
          {"source":dst,"target":src,"label":revlabel,"style":f"edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#222222;endArrow=block;endFill=1;fontFamily=Arial;fontSize=12;labelBackgroundColor=#FFFFFF;exitX=0;exitY={offset2};entryX=1;entryY={offset2};"},
        ]
    arch_edges=[]
    arch_edges+=pair("browser","fe-zone","Request","Response","0.38","0.62")
    arch_edges+=pair("fe-zone","api-label","Request","Response","0.42","0.58")
    arch_edges+=pair("api-label","gemini-label","Request","Response","0.42","0.60")
    arch_edges+=pair("api-label","speech-label","Request","Response","0.58","0.42")
    arch_edges+=pair("data-layer","sql-label","SQL request","DB response","0.65","0.35")
    emit_drawio(DOCS/"AIVES-System-Architecture-remake.drawio","AIVES System Architecture (remake)",arch_nodes,arch_edges,width=1800,height=1180)


def update_report():
    doc=Document(REPORT)
    # Replace body text while retaining paragraph styles and document layout.
    replacements={
      "This report describes the AIVES architecture and separates the software implemented for Milestone 3 from the planned viva and grading workflows. The current backend provides the question bank, authentication, and AI question-generation foundation.":
      "This report describes the AIVES architecture and the implemented Milestone 3 workflows. The backend supports question and course-rubric management, exam sessions and student attempts, text and voice interview paths, AI-assisted grading, lecturer finalization, and result retrieval. Provider credentials and live Azure SQL deployment still require environment-level verification.",
      "AIVES is intended to support oral examinations with a consistent question bank, structured scoring rubrics, and future AI-assisted interview and grading capabilities. This document records the current backend structure and the design direction for the next workflows.":
      "AIVES supports oral examinations through a question bank, course-level scoring rubrics, scheduled sessions, student attempts, and AI-assisted interview and grading. This report documents the current repository implementation and distinguishes code-level support from integrations that have not been live-tested.",
      "The implementation statements in this report are based on the project source tree, backend contracts, and current diagrams. Proposed entities and workflows are labeled as target design. Deployment services, performance targets, and production readiness are not claimed unless they are demonstrated by the repository.":
      "Implementation statements are based on the current project source and backend contracts. The diagrams describe current code behavior unless explicitly labeled as target deployment. Gemini, Azure AI Speech, and Azure SQL credentials or live service behavior have not been verified end-to-end in this report; production readiness and performance are not claimed.",
      "A lecturer submits question content, course and Bloom level, together with one or more rubric criteria. The application validates the request and active course, then persists the question and rubric rows through the unit of work. Rubric weights must total 100%, with the backend tolerance documented as less than 0.01. Invalid requests return validation errors. Updating a question replaces its active rubric matrix; deleting a question soft-deletes the question and its rubrics.":
      "A lecturer manages questions and course-level rubrics through separate endpoints. Question create/update validates content, course, and Bloom level; rubric replacement uses PUT /api/courses/{courseId}/rubrics, validates the rubric matrix, soft-deletes the previous active rows, and saves the replacement set. Rubric weights total 100% within the backend tolerance of 0.01. Question deletion soft-deletes the question; it does not delete the course rubric matrix.",
      "The state machine uses Draft, Validating, Saved, Updating, Validating (Update), and SoftDeleted as workflow states. The Question entity has no persisted Status field.":
      "Question authoring states describe request processing, not a persisted Question.Status field. ExamSession and Transcript have persisted lifecycle states and are shown in the state model.",
      "The target sequence models a student joining an interview room, receiving a question, answering by voice, and repeating bounded follow-up turns before submitting. The proposed design includes speech-to-text, text-to-speech, transcript turns, question progression, and a submission event. These steps are design scope and should be verified against implementation before being described as operational.":
      "The implemented backend supports session creation/opening, student attempt start/resume, question and turn retrieval, text answers, AI follow-up, voice-answer upload, transcript persistence, and submission. Voice answers use Azure AI Speech for transcription, Gemini for follow-up, and Azure AI Speech for synthesized speech. The full browser-to-provider-to-Azure-SQL path still needs live environment verification.",
      "The target sequence evaluates a submitted transcript against question rubrics, produces suggested scores and feedback, and presents them to a lecturer. The lecturer may accept or adjust the proposed scores before finalization. The lecturer remains responsible for the official result. Assessment storage, finalization audit data, and this complete workflow are not claimed as implemented in Milestone 3.":
      "The implemented grading flow is initiated by a lecturer for a submitted attempt. The backend stores rubric-based AI suggestions, feedback, and confidence per session question; a lecturer then finalizes each score with an optional comment. The official attempt total is stored only after every assessment is finalized. The result endpoint separates the AI suggested total from the official total.",
      "The use case diagram summarizes the product scope across lecturer, student, administrator, and external AI roles. It includes implemented question authoring and administrative use cases alongside planned interview and grading use cases. Read it as a system-level scope model, not as a delivery checklist.":
      "The use case diagram summarizes lecturer and student actions and external AI/speech integrations. Administrator-only actions are excluded because the current authorization model has Lecturer and Student roles; external providers are supporting actors, not application users.",
      "The solution is organized into four projects. Dependencies point toward the domain and application contracts; infrastructure and the API host provide adapters at the outer boundary. The architecture overview is shown in Figure 5.":
      "The solution is organized into Domain, Application, Infrastructure, and WebApi projects. Dependencies point toward domain and application contracts; infrastructure and the API host provide adapters at the outer boundary. Figure 5 summarizes the technologies and external services present in the current repository.",
      "For question creation, the API controller accepts a request and invokes the application service. Validators check the request before persistence. The service coordinates the question repository and unit of work; Entity Framework Core commits the entity graph to SQL Server. DTO mapping keeps persistence entities separate from the HTTP response shape.":
      "For question creation, the API controller invokes QuestionBankService, which validates the request, checks the active course, and persists the Question through the unit of work and Entity Framework Core. Course rubric replacement is a separate CourseService operation. DTO mapping keeps persistence entities separate from HTTP response contracts.",
      "The revised conceptual ERD adds ExamSession, ExamSessionQuestion, Transcript, TranscriptTurn, and Assessment to represent scheduled sessions, a stable question set, dialogue turns, and per-question grading. These are target-model entities and must not be presented as tables already deployed in the current backend.":
      "The conceptual ERD includes Course, Question, Rubric, ExamSession, ExamSessionQuestion, Transcript, TranscriptTurn, Assessment, and User. These entities are present in the current code model and migrations; deployment of the latest migrations to the shared Azure SQL database has not been verified.",
      "The state machine covers the implemented Flow 1 lifecycle, including create validation, saved questions, update validation, and soft deletion. Invalid requests return to editing with errors. Valid requests are persisted; updates replace active rubrics. “Draft” and “Validating” describe request-processing steps and are not database states.":
      "The state diagrams show the persisted ExamSession and Transcript state transitions. Draft and validation steps for question authoring are workflow concepts; they are not persisted Question status values. Course rubrics are managed independently from Question lifecycle.",
      "This report includes one state machine because the current Milestone 3 implementation centers on question authoring. Interview-session and grading lifecycles belong to the target model and are not combined into this implemented state diagram.":
      "The report includes the session lifecycle (Draft, Open, Closed, Cancelled) and the student-attempt lifecycle (InProgress, Submitted, Grading, AwaitingReview, Finalized). These values correspond to the current domain enums.",
      "The repository contains backend check projects for question-bank rules, rubric validation, course behavior, authentication, authorization, and SQL Server integration. Their presence indicates available verification coverage; this report does not claim that checks were executed or passed during this document update.":
      "The backend progress record reports 436 checks passing on 31 October 2026 and a Release build with zero errors. Those checks use local test infrastructure; Gemini, Azure AI Speech, and Azure SQL live flows remain unverified.",
      "The interview and grading diagrams are design artifacts. Confirm endpoint, persistence, and integration behavior in code before marking these workflows complete.":
      "The interview and grading diagrams describe backend routes and behavior present in code. Live provider credentials, browser microphone behavior, and applying the latest migrations to Azure SQL remain separate verification tasks.",
      "The conceptual ERD contains planned entities beyond the current database context.":
      "The conceptual ERD intentionally omits PK/FK and implementation data types; relationship cardinalities are shown at a business level.",
      "Milestone 3 establishes the AIVES backend foundation and the question-bank workflow: validated question and rubric authoring, persistence, course and user capabilities, authentication, and AI-assisted question generation. The live viva and human-in-the-loop grading flows provide a direction for subsequent milestones, while their implementation status remains distinct from the current question-authoring state machine.":
      "Milestone 3 now includes question and course-rubric management, exam-session and transcript lifecycles, text and voice interview APIs, AI-assisted per-question grading, lecturer finalization, and result retrieval. The diagrams distinguish these implemented backend workflows from deployment assumptions and live provider/database checks that still require confirmation.",
    }
    for p in doc.paragraphs:
        old=p.text.strip()
        if old in replacements:
            p.text=replacements[old]
        if old.startswith("This report describes the AIVES architecture"):
            p.text="This report describes the AIVES architecture and the implemented Milestone 3 workflows. The backend supports question and course-rubric management, exam sessions and student attempts, text and voice interview APIs, AI-assisted grading, lecturer score finalization, and result retrieval. Live Gemini, Azure AI Speech, and Azure SQL deployment still require environment-level verification."
        elif old.startswith("AIVES is intended to support oral examinations"):
            p.text="AIVES supports oral examinations through a question bank, course-level scoring rubrics, scheduled sessions, student attempts, and AI-assisted interview and grading. This report documents current repository implementation and separates it from deployment and provider verification."
        elif old.startswith("The implementation statements in this report are based on the project source tree"):
            p.text="Implementation statements are based on the project source, backend contracts, and progress notes. The diagrams describe current code behavior unless marked otherwise. Gemini, Azure AI Speech, and Azure SQL live behavior has not been verified end-to-end; production readiness and performance are not claimed."
        elif old=="This report describes the AIVES architecture and separates the software implemented for Milestone 3 from the planned viva and grading workflows. The current backend provides the question bank, rubric validation, course and user management, authentication, and AI-assisted question generation. The live interview and human-reviewed AI grading diagrams are target designs; their presence in the models does not mean those features are implemented.":
            p.text="This report describes the AIVES architecture and the implemented Milestone 3 workflows. The backend supports question and course-rubric management, exam sessions and student attempts, text and voice interview APIs, AI-assisted grading, lecturer score finalization, and result retrieval. Live Gemini, Azure AI Speech, and Azure SQL deployment still require environment-level verification."
        elif old=="The use case diagram includes both current and planned capabilities; use implementation status notes when presenting it.":
            p.text="The use case diagram summarizes the current lecturer and student scope and the external AI/speech integrations described in the workflow sections."

    # Accurate workflow headings and diagram/caption order.
    heading_replacements={
      "2.2 Target workflow: live viva interview (Flow 3)":"2.2 Implemented workflow: live viva interview (Flow 3)",
      "2.3 Target workflow: AI grading and human review (Flow 4)":"2.3 Implemented workflow: AI grading and lecturer review (Flow 4)",
      "5.2 Target examination model":"5.2 Conceptual data relationships",
      "6.1 Question authoring state machine":"6.1 Session and attempt state machines",
    }
    for p in doc.paragraphs:
        if p.text.strip() in heading_replacements: p.text=heading_replacements[p.text.strip()]

    # Replace embedded legacy pictures by placing all eight current figures before their captions.
    from docx.oxml import OxmlElement
    from docx.text.paragraph import Paragraph
    for p in list(doc.paragraphs):
        if p._p.xpath('.//w:drawing'):
            p._p.getparent().remove(p._p)
    # Move Figure 6/7 to the data-model section, before the dynamic model.
    by_caption={p.text.strip():p for p in doc.paragraphs if p.text.strip().startswith("Figure ")}
    f6=by_caption.get("Figure 6. AIVES conceptual ERD")
    f7=by_caption.get("Figure 7. Core AIVES domain class model")
    section6=next((p for p in doc.paragraphs if p.text.strip()=="6. Dynamic Model"),None)
    if f6 and f7 and section6:
        section6._p.addprevious(f6._p)
        section6._p.addprevious(f7._p)
    captions={
      "Figure 1. Sequence diagram for question and course-rubric management (Flow 1)":"07_Sequence_Flow1_Current.png",
      "Figure 2. Sequence diagram for the viva interview workflow (Flow 3)":"08_Sequence_Flow3_Current.png",
      "Figure 3. Sequence diagram for AI grading and lecturer finalization (Flow 4)":"09_Sequence_Flow4_Current.png",
      "Figure 4. AIVES use case overview":"02_UseCase_Current.png",
      "Figure 5. Current system architecture and external integrations":"01_System_Architecture_Current.png",
      "Figure 6. AIVES conceptual ERD":"03_Conceptual_ERD_Current.png",
      "Figure 7. Core AIVES domain class model":"04_Class_Diagram_Current.png",
      "Figure 8. Session and student-attempt state machines":"05_State_Machines_Current.png",
    }
    for p in list(doc.paragraphs):
        key=p.text.strip()
        if key not in captions: continue
        pxml=OxmlElement('w:p'); p._p.addprevious(pxml)
        ip=Paragraph(pxml,p._parent); ip.alignment=1
        width=Inches(6.4)
        if key.startswith("Figure 5."): width=Inches(5.45)
        # Trim outer white canvas from diagram renders so the actual notation remains
        # readable in Word. Keep the full PNG beside it as the unmodified render source.
        from PIL import Image, ImageChops
        source=IMAGES/captions[key]
        with Image.open(source).convert("RGB") as image:
            diff=ImageChops.difference(image,Image.new("RGB",image.size,"white"))
            bbox=diff.point(lambda value: 255 if value > 16 else 0).getbbox()
            if bbox:
                left,top,right,bottom=bbox
                pad=28
                bbox=(max(0,left-pad),max(0,top-pad),min(image.width,right+pad),min(image.height,bottom+pad))
                image.crop(bbox).save(IMAGES/(source.stem+"_Report.png"))
                source=IMAGES/(source.stem+"_Report.png")
        ip.add_run().add_picture(str(source),width=width)
        if key.startswith(("Figure 1.","Figure 2.","Figure 3.","Figure 4.")):
            ip.paragraph_format.page_break_before=True
        if key.startswith("Figure 7."):
            ip.paragraph_format.page_break_before=True
        p.alignment=1
        for run in p.runs: run.bold=True; run.font.size=Pt(9)

    # Refresh report tables so the diagrams and narrative agree with current source.
    def cell(table,row,col,text):
        c=table.cell(row,col); c.text=text
    if len(doc.tables)>0:
        t=doc.tables[0]
        vals=[
          ("Question and course rubric management","Implemented: separate question and course-rubric APIs; validation, persistence, update, and soft deletion."),
          ("Users, courses, authentication","Implemented backend capabilities; current roles are Lecturer and Student."),
          ("Live viva interview (Flow 3)","Backend routes and services are implemented for text/voice attempts, follow-up, transcript turns, and submission; live provider/browser integration still needs environment verification."),
          ("AI grading and lecturer finalization (Flow 4)","Backend supports lecturer-triggered AI grading, per-question assessments, score finalization, and result retrieval; external live services are not verified here."),
          ("State model","ExamSession and Transcript lifecycle enums are persisted domain states; Question authoring Draft/Validating are request workflow concepts."),
        ]
        for i,(a,b) in enumerate(vals,1): cell(t,i,0,a);cell(t,i,1,b)
    if len(doc.tables)>1:
        t=doc.tables[1]
        while len(t.rows)>4: t._tbl.remove(t.rows[-1]._tr)
        rows=[("Lecturer","Manages questions, course rubrics, exam sessions, grading, and final scores."),
              ("Student","Starts an exam attempt, submits text or voice answers, and views finalized results."),
              ("Third-party services","Google Gemini and Azure AI Speech provide AI and speech-processing capabilities; Azure SQL is the managed database target.")]
        for i,(a,b) in enumerate(rows,1):cell(t,i,0,a);cell(t,i,1,b)
    if len(doc.tables)>2:
        t=doc.tables[2]
        for i,(name,status) in enumerate([
          ("Sequence-CreateQuestion-Rubric-remake-v2","Implemented Flow 1; question and course-rubric endpoints are separate."),
          ("Sequence-Flow3-AIViva-Interview-remake-v2","Implemented backend flow; live provider and browser integration still requires environment verification."),
          ("Sequence-Flow4-AIGrading-HumanInTheLoop-remake-v2","Implemented backend flow; live external-service behavior is not verified here."),
        ],1):
            cell(t,i,0,name);cell(t,i,1,status)
            cell(t,i,2,"Implemented" if i==1 else "Implemented; live integrations unverified")
    if len(doc.tables)>3:
        cell(doc.tables[3],2,1,"Rubric weights are validated and rubric replacement is a separate Course-level operation; it is not part of Question persistence.")
    if len(doc.tables)>4:
        cell(doc.tables[4],3,1,"EF Core persistence and SQL Server provider; Azure SQL is the target managed deployment and live connection has not been verified here.")
    if len(doc.tables)>5:
        t=doc.tables[5]
        rows=[("Frontend framework","React 19 and Vite 8"),("UI components","Ant Design 6"),("HTTP client","Axios"),
              ("Backend runtime","ASP.NET Core / .NET 10"),("Persistence","Entity Framework Core 10 and SQL Server provider"),
              ("API documentation","Swagger / OpenAPI"),("Authentication","JWT bearer authentication and password hashing service"),
              ("AI services","Google Gemini integration"),("Speech services","Azure AI Speech (STT / TTS)"),("Database target","Azure SQL Database; live connection not verified")]
        while len(t.rows)<len(rows)+1:t.add_row()
        for i,(a,b) in enumerate(rows,1):cell(t,i,0,a);cell(t,i,1,b)
    if len(doc.tables)>6:
        cell(doc.tables[6],2,1,"Course owns questions and rubric criteria; rubrics are managed at Course level.")
        cell(doc.tables[6],3,1,"Course-linked question with Bloom level and audit fields; it does not own the course rubric matrix.")
        cell(doc.tables[6],4,1,"Course-level scoring criterion with weight, maximum score, and audit fields.")
    if len(doc.tables)>7:
        t=doc.tables[7]; t.cell(0,0).text="Entity"; t.cell(0,1).text="Role in the current model"
        vals=[("ExamSession","Scheduled viva session with persisted lifecycle state."),
              ("ExamSessionQuestion","Ordered question and rubric snapshot used for an attempt."),
              ("Transcript","One student attempt in a session."),
              ("TranscriptTurn","Ordered dialogue entry with speaker, content, and timestamp."),
              ("Assessment","Per-question AI suggestion and lecturer-finalized score.")]
        for i,(a,b) in enumerate(vals,1):cell(t,i,0,a);cell(t,i,1,b)
    # Replace references with the preserved original + clearly named remake sources.
    refs=[
      "Original sources are retained beside the updated remake diagrams for comparison and further editing.",
      "docs/AIVES-System-Architecture-remake.drawio — current stack, third-party integrations, and request/response paths.",
      "docs/UseCase-remake.drawio — current roles and use cases.",
      "docs/Conceptual-ERD-AIVES-remake.drawio — business entities and cardinalities, without PK/FK fields.",
      "docs/AIVES-UML-Class-Diagram-remake.drawio — current implementation class model.",
      "docs/Sequence-CreateQuestion-Rubric-remake-v2.drawio — Flow 1.",
      "docs/Sequence-Flow3-AIViva-Interview-remake-v2.drawio — Flow 3.",
      "docs/Sequence-Flow4-AIGrading-HumanInTheLoop-remake-v2.drawio — Flow 4.",
      "docs/State-Machine-Flow1-Implemented-remake.drawio and docs/State-Machine-Exam-Lifecycle-remake.drawio — current state diagrams.",
      "BACKEND_CONTRACTS.md and BACKEND_PROGRESS.md — API contract and implementation record.",
    ]
    appendix=next((p for p in doc.paragraphs if p.text.strip()=="Appendix A. Project references"),None)
    if appendix:
        # Remove prior reference paragraphs until document end; this appendix is the final section.
        for p in list(doc.paragraphs):
            if p._p.getprevious() is not None and p._p.getprevious() is appendix._p:
                pass
        body=doc._body._element
        children=list(body)
        start=children.index(appendix._p)
        for element in children[start+1:]:
            if element.tag.endswith('}p'): body.remove(element)
        for text in refs:
            pxml=OxmlElement('w:p'); body.insert(len(body)-1,pxml); Paragraph(pxml,appendix._parent).add_run(text)
    for p in doc.paragraphs:
        if p.text.strip().startswith("The current context and migrations cover these core data areas."):
            p.text="The current context and migrations cover these core data areas. A Course owns its Questions and Rubric criteria; each ExamSessionQuestion stores the question and rubric snapshot used for an attempt. Rubric weights are validated as a complete course-level scoring matrix."
    doc.save(REPORT)


def legacy_main():
    arch=architecture(); usecase_img=usecase(); erd_img=erd(); class_img=class_diagram()
    flow1=sequence("SEQUENCE DIAGRAM — QUESTION AND COURSE RUBRIC MANAGEMENT (FLOW 1)","Actual API responsibilities; rubric belongs to Course, not Question",
        ["Lecturer","Frontend","QuestionsController / CoursesController","Validators","Application services","EF Core / SQL Server"],
        [(0,1,"Enter question or course rubric",False),(1,2,"POST /api/questions",False),(2,3,"Validate question + active course",False),(3,4,"CreateQuestionAsync",False),(4,5,"Save Question",False),(5,4,"Question saved",True),(4,3,"201 Created + QuestionDto",True),(0,1,"Replace course rubric matrix",False),(1,2,"PUT /api/courses/{id}/rubrics",False),(2,3,"Validate criteria and total weight = 100%",False),(3,4,"ReplaceRubricsAsync",False),(4,5,"Soft-delete old rows; insert replacement rows",False),(5,4,"Commit",True),(4,3,"200 OK + CourseDto",True)],"07_Sequence_Flow1_Current.png")
    flow3=sequence("SEQUENCE DIAGRAM — VIVA INTERVIEW (FLOW 3)","Current backend: REST attempts and follow-up; Azure AI Speech is used only for voice answers",
        ["Student","Web client","Attempts / Voice API","AttemptService","Google Gemini API","Azure AI Speech","Database"],
        [(0,1,"Start or resume attempt",False),(1,2,"POST /api/exam-sessions/{id}/attempts",False),(2,3,"Check student, session, existing attempt",False),(3,6,"Persist Transcript(InProgress)",False),(6,3,"AttemptDto + question snapshot",True),(2,1,"201 Created / 200 OK",True),(1,2,"GET /api/attempts/{id}/questions",False),(2,6,"Load ordered question snapshot",False),(6,2,"Question list",True),(2,1,"Return questions",True),(1,2,"POST /api/attempts/{id}/answers",False),(2,3,"Validate answer and active question",False),(3,6,"Save student TranscriptTurn",False),(6,3,"Saved turn",True),(1,2,"POST /api/attempts/{id}/follow-up",False),(2,3,"Build follow-up context from saved turns",False),(3,4,"Gemini HTTPS request",False),(4,3,"Follow-up question",True),(3,6,"Save AI TranscriptTurn",False),(3,2,"Return follow-up",True),(2,1,"HTTP response",True),(1,2,"Optional: POST /api/attempts/{id}/voice-answers (audio)",False),(2,3,"Validate audio and attempt",False),(3,5,"Speech-to-text request",False),(5,3,"Recognized answer text",True),(3,6,"Save student TranscriptTurn",False),(3,4,"Gemini follow-up request",False),(4,3,"Follow-up text",True),(3,5,"Optional text-to-speech request",False),(5,3,"Follow-up audio",True),(3,6,"Save AI TranscriptTurn",False),(3,2,"Return transcript and optional audio",True),(2,1,"HTTP response",True),(1,2,"POST /api/attempts/{id}/submit",False),(2,3,"Set Submitted",False),(3,6,"Persist submission",False),(6,3,"Saved",True),(2,1,"HTTP response",True)],"08_Sequence_Flow3_Current.png")
    flow4=sequence("SEQUENCE DIAGRAM — AI GRADING AND LECTURER FINALIZATION (FLOW 4)","Lecturer triggers grading; official total waits for every question to be finalized",
        ["Lecturer","Review UI","Grading / Assessments APIs","GradingService","Google Gemini API","Database","Student"],
        [(0,1,"Select submitted attempt and request grading",False),(1,2,"POST /api/attempts/{id}/grade",False),(2,3,"Require lecturer owner; require Submitted",False),(3,5,"Load turns and frozen question/rubric snapshots",False),(5,3,"Transcript and rubric snapshots",True),(3,4,"Request structured rubric scores",False),(4,3,"Scores, feedback, confidence",True),(3,3,"Validate response; calculate score per rubric",False),(3,5,"Persist Assessment rows; AwaitingReview",False),(5,3,"Saved",True),(2,1,"202 Accepted + assessments",True),(1,2,"GET /api/attempts/{id}/assessments",False),(2,5,"Load assessments in snapshot order",False),(5,2,"Assessment list",True),(2,1,"Return assessments",True),(0,1,"Enter final score/comment for each assessment",False),(1,2,"PUT /api/assessments/{id}/final-score + version",False),(2,3,"Authorize lecturer; finalize this question",False),(3,5,"Save final score and audit fields",False),(5,3,"Persisted",True),(3,3,"When all finalized: calculate official total; Finalized",False),(1,2,"GET /api/attempts/{id}/result",False),(2,5,"Load finalized result",False),(5,2,"Official result",True),(2,1,"Return result",True),(6,1,"Request finalized result",False),(1,2,"GET /api/attempts/{id}/result",False),(2,5,"Load finalized result",False),(5,2,"Official result",True),(2,1,"Return result",True),(1,6,"Display official result",False)],"09_Sequence_Flow4_Current.png")
    state=state_machine()
    install_drawio_sources()
    install_usecase_source()
    install_class_source()
    # Save the two current state diagrams and the accurate main sequence files using the same source format.
    state_nodes=[{"id":x,"label":lab,"x":xx,"y":yy,"w":220,"h":80} for x,lab,xx,yy in [("draft","Draft",160,180),("validating","Validating",480,180),("saved","Saved",810,180),("updating","Updating",810,370),("deleted","SoftDeleted",1160,370)]]
    state_edges=[{"source":a,"target":b,"label":l} for a,b,l in [("draft","validating","submit create"),("validating","draft","invalid / HTTP 400"),("validating","saved","valid / persist Question"),("saved","updating","edit"),("updating","validating","submit update"),("updating","saved","valid update"),("saved","deleted","DELETE /api/questions/{id}")]]
    emit_drawio(DOCS/"State-Machine-Flow1-Implemented-remake.drawio","Flow 1 - Question authoring workflow (remake)",state_nodes,state_edges)
    # Editable sequence diagrams mirror the sequence figures above.
    emit_sequence(DOCS/"Sequence-CreateQuestion-Rubric-remake-v2.drawio","Flow 1 - Question and course rubric (remake v2)",
      ["Lecturer","Frontend","Questions / Courses API","Validators","Application services","SQL Server"],
      [(0,1,"Enter question or rubric",False),(1,2,"POST /api/questions",False),(2,3,"Validate content, course, Bloom level",False),(3,4,"CreateQuestionAsync",False),(4,5,"Persist Question",False),(5,4,"Saved",True),(4,2,"201 Created + QuestionDto",True),(0,1,"Replace course rubric",False),(1,2,"PUT /api/courses/{id}/rubrics",False),(2,3,"Validate criteria and total weight",False),(3,4,"ReplaceRubricsAsync",False),(4,5,"Soft-delete old rows; insert new rows",False),(5,4,"Commit",True),(4,2,"200 OK + CourseDto",True)])
    emit_sequence(DOCS/"Sequence-Flow3-AIViva-Interview-remake-v2.drawio","Flow 3 - Viva interview (remake v2)",
      ["Student","Web client","Attempts / Voice API","AttemptService","Google Gemini API","Azure AI Speech","SQL Server"],
      [(0,1,"Start or resume attempt",False),(1,2,"POST /api/exam-sessions/{id}/attempts",False),(2,3,"Check student, session, existing attempt",False),(3,6,"Persist Transcript(InProgress)",False),(6,3,"AttemptDto + question snapshot",True),(2,1,"201 Created / 200 OK",True),(1,2,"GET /api/attempts/{id}/questions",False),(2,6,"Load ordered question snapshot",False),(6,2,"Question list",True),(2,1,"Return questions",True),(1,2,"POST /api/attempts/{id}/answers",False),(2,3,"Validate answer and active question",False),(3,6,"Save student TranscriptTurn",False),(6,3,"Saved turn",True),(1,2,"POST /api/attempts/{id}/follow-up",False),(2,3,"Build context from saved turns",False),(3,4,"Gemini HTTPS request",False),(4,3,"Follow-up question",True),(3,6,"Save AI TranscriptTurn",False),(3,2,"Return follow-up",True),(2,1,"HTTP response",True),(1,2,"Optional: POST voice-answers (audio)",False),(2,3,"Validate audio and attempt",False),(3,5,"Speech-to-text request",False),(5,3,"Recognized text",True),(3,6,"Save student turn",False),(3,4,"Gemini follow-up request",False),(4,3,"Follow-up text",True),(3,5,"Optional text-to-speech request",False),(5,3,"Follow-up audio",True),(3,6,"Save AI turn",False),(3,2,"Return transcript + optional audio",True),(2,1,"HTTP response",True),(1,2,"POST /api/attempts/{id}/submit",False),(2,3,"Set Submitted",False),(3,6,"Persist submission",False),(2,1,"HTTP response",True)])
    emit_sequence(DOCS/"Sequence-Flow4-AIGrading-HumanInTheLoop-remake-v2.drawio","Flow 4 - AI grading (remake v2)",
      ["Lecturer","Review UI","Grading / Assessments API","GradingService","Google Gemini API","SQL Server","Student"],
      [(0,1,"Select submitted attempt",False),(1,2,"POST /api/attempts/{id}/grade",False),(2,3,"Authorize owner; require Submitted",False),(3,5,"Load turns and rubric snapshots",False),(5,3,"Transcript and rubric snapshots",True),(3,4,"Request structured criterion scores",False),(4,3,"Scores, feedback, confidence",True),(3,3,"Validate output and calculate score",False),(3,5,"Persist assessments; AwaitingReview",False),(5,3,"Saved",True),(2,1,"202 Accepted + assessments",True),(1,2,"GET /api/attempts/{id}/assessments",False),(2,5,"Load assessments in question order",False),(5,2,"Assessment list",True),(2,1,"Return assessments",True),(0,1,"Enter final score/comment per question",False),(1,2,"PUT /api/assessments/{id}/final-score",False),(2,3,"Check owner, version and score range",False),(3,5,"Save final score and lecturer audit",False),(5,3,"Persisted",True),(3,3,"When all finalized: store total; Finalized",False),(1,2,"GET /api/attempts/{id}/result",False),(2,5,"Load finalized result",False),(5,2,"Official result",True),(2,1,"Return result",True),(6,1,"Request finalized result",False),(1,2,"GET /api/attempts/{id}/result",False),(2,5,"Load finalized result",False),(5,2,"Official result",True),(2,1,"Return result",True),(1,6,"Display official result",False)])
    update_report()


def main():
    from rebuild_reviewed_diagrams import main as rebuild_reviewed
    rebuild_reviewed()
    architecture()
    usecase()
    install_usecase_source()
    update_report()


if __name__ == "__main__":
    main()
