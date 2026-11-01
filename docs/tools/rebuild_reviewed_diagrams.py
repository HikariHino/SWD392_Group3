from __future__ import annotations

import math
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
IMAGES = DOCS / "Report_Images"
FONT = Path(r"C:\Windows\Fonts\arial.ttf")
FONT_B = Path(r"C:\Windows\Fonts\arialbd.ttf")
INK = (23, 29, 37)
MID = (85, 95, 105)
PAPER = (255, 255, 255)


def f(size: int, bold=False):
    return ImageFont.truetype(str(FONT_B if bold else FONT), size)


def wrap(draw, text, font, width):
    lines = []
    for para in text.split("\n"):
        row = ""
        for word in para.split():
            trial = (row + " " + word).strip()
            if draw.textbbox((0, 0), trial, font=font)[2] <= width:
                row = trial
            else:
                if row:
                    lines.append(row)
                row = word
        lines.append(row)
    return lines


def canvas(title, subtitle, w=1800, h=1180):
    im = Image.new("RGB", (w, h), PAPER)
    d = ImageDraw.Draw(im)
    d.text((w / 2, 32), title, font=f(30, True), fill=INK, anchor="mt")
    d.text((w / 2, 78), subtitle, font=f(16), fill=MID, anchor="mt")
    d.line((55, 120, w - 55, 120), fill=(175, 182, 189), width=2)
    return im, d


def card(d, xy, title, body, w=300, h=110, size=18):
    x, y = xy
    d.rounded_rectangle((x, y, x+w, y+h), radius=12, fill=(249, 250, 251), outline=INK, width=2)
    d.text((x+w/2, y+14), title, font=f(size, True), fill=INK, anchor="mt")
    lines = wrap(d, body, f(14), w-24)
    yy = y+54
    for line in lines[:2]:
        d.text((x+w/2, yy), line, font=f(14), fill=INK, anchor="mt")
        yy += 19


def connector(d, points, label, label_at=None, dashed=False, color=INK, fs=13, arrow=True):
    for a, b in zip(points, points[1:]):
        x1, y1 = a; x2, y2 = b
        if dashed:
            dist = max(1, math.hypot(x2-x1, y2-y1)); ux=(x2-x1)/dist; uy=(y2-y1)/dist
            pos=0
            while pos < dist:
                end=min(pos+12, dist)
                d.line((x1+ux*pos,y1+uy*pos,x1+ux*end,y1+uy*end), fill=color, width=2)
                pos += 21
        else:
            d.line((x1,y1,x2,y2), fill=color, width=2)
    if arrow and len(points) > 1:
        a,b=points[-2],points[-1]; ang=math.atan2(b[1]-a[1],b[0]-a[0]); L=11
        p=(b[0]-L*math.cos(ang-.45), b[1]-L*math.sin(ang-.45))
        q=(b[0]-L*math.cos(ang+.45), b[1]-L*math.sin(ang+.45))
        d.polygon([b,p,q], fill=color)
    if label:
        if label_at is None:
            a,b=points[0],points[-1]; label_at=((a[0]+b[0])/2,(a[1]+b[1])/2-14)
        x,y=label_at; ff=f(fs, True)
        lines=wrap(d,label,ff,320)
        text="\n".join(lines)
        bb=d.multiline_textbbox((x,y),text,font=ff,anchor="mm",align="center",spacing=1)
        d.rectangle((bb[0]-4,bb[1]-3,bb[2]+4,bb[3]+3),fill=PAPER)
        d.multiline_text((x,y),text,font=ff,fill=color,anchor="mm",align="center",spacing=1)


def make_erd():
    im,d=canvas("AIVES CONCEPTUAL ERD","Business entities and relationship names / cardinalities are shown on each connector")
    p={"Rubric":(60,175),"Course":(600,175),"Question":(1140,175),"User":(60,430),"ExamSession":(600,430),
       "SessionQuestion":(1140,430),"Transcript":(60,690),"TranscriptTurn":(600,690),"Assessment":(1140,690)}
    names={"Rubric":"Scoring criteria owned by a course","Course":"Subject and shared rubric owner","Question":"Question-bank item linked to a course",
           "User":"Lecturer or student account","ExamSession":"Scheduled viva session","SessionQuestion":"Ordered question and rubric snapshot",
           "Transcript":"One student attempt in a session","TranscriptTurn":"Ordered AI / student dialogue turn","Assessment":"Per-question AI and lecturer score"}
    # Connectors are drawn first, so no line can cover entity text. Labels sit directly on the connector.
    routes=[
      ("Course","Question","1 — questions — 0..*",[(900,230),(1140,230)],(1020,211),False),
      ("Course","Rubric","1 — defines — 0..*",[(600,175),(600,145),(210,145),(210,175)],(405,145),False),
      ("Course","ExamSession","1 — schedules — 0..*",[(750,230),(820,320),(820,405),(750,485)],(820,355),False),
      ("User","ExamSession","1 lecturer — creates — 0..*",[(360,485),(600,485)],(480,466),False),
      ("ExamSession","SessionQuestion","1 — contains — 1..*",[(900,485),(1140,485)],(1020,466),False),
      ("Question","SessionQuestion","1 — appears in — 0..*",[(1290,285),(1290,430)],(1290,357),False),
      ("User","Transcript","1 Student — submits — 0..* Attempts",[(210,540),(210,690)],(210,612),False),
      ("ExamSession","Transcript","1 — records — 0..*",[(700,540),(700,640),(360,640),(360,745)],(530,640),False),
      ("Transcript","TranscriptTurn","1 — turns — 0..*",[(360,745),(600,745)],(480,726),False),
      ("Transcript","Assessment","1 — has — 0..*",[(360,790),(405,825),(1085,825),(1140,790)],(745,825),False),
      ("SessionQuestion","Assessment","1 — scored in — 0..*",[(1290,540),(1290,690)],(1290,612),False),
      ("SessionQuestion","TranscriptTurn","1 — referenced by — 0..*",[(1140,515),(1100,560),(1100,610),(900,610),(750,690)],(1000,610),False),
      ("User","Assessment","0..1 lecturer — finalizes — 0..*",[(60,540),(35,570),(35,875),(1100,875),(1290,800)],(570,875),True),
    ]
    for a,b,label,pts,at,dash in routes:
        connector(d,pts,label,at,dashed=dash,fs=12,arrow=False)
    for n,xy in p.items(): card(d,xy,n,names[n])
    out=IMAGES/"03_Conceptual_ERD_Current.png"; im.save(out,optimize=True)
    # Matching editable draw.io source, with cardinality/relationship text on edges.
    nodes=[{"id":n,"label":f"<b>{n}</b><br>{names[n]}","x":xy[0],"y":xy[1],"w":300,"h":110,
            "style":"rounded=1;whiteSpace=wrap;html=1;fillColor=#F9FAFB;strokeColor=#171D25;fontColor=#171D25;fontFamily=Arial;fontSize=14;"} for n,xy in p.items()]
    edges=[]
    for i,(a,b,label,pts,at,dash) in enumerate(routes):
        style="edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#171D25;endArrow=none;startArrow=none;fontFamily=Arial;fontSize=12;labelBackgroundColor=#FFFFFF;"
        if dash: style += "dashed=1;dashPattern=6 4;"
        edges.append({"id":f"rel{i}","source":a,"target":b,"label":label,"style":style})
    write_drawio(DOCS/"Conceptual-ERD-AIVES-remake.drawio","AIVES conceptual ERD",nodes,edges,1800,980)


def make_class_diagram():
    im,d=canvas("AIVES DOMAIN CLASS DIAGRAM","Current domain properties; association names and multiplicities appear on connectors",1800,1370)
    p={"User":(60,430),"Course":(600,170),"Question":(1140,170),"Rubric":(60,170),
       "ExamSession":(600,430),"SessionQuestion":(1140,430),"Transcript":(60,690),"TranscriptTurn":(600,690),"Assessment":(1140,690)}
    fields={"User":"Username · FullName\nRole · IsDeleted","Course":"Code · Name\nDescription? · IsDeleted",
      "Question":"Content · BloomLevel\nPoints · ExpectedAnswer?","Rubric":"Criteria · Weight\nMaxScore · AiGradingPrompt?",
      "ExamSession":"SessionTitle · Start/EndTime\nStatus · Version","SessionQuestion":"DisplayOrder · Content · Points\nRubricSnapshotJson",
      "Transcript":"Status · StartedAt · SubmittedAt?\nTotalScore? · Version","TranscriptTurn":"Sequence · Speaker\nContentText · Timestamp",
      "Assessment":"StudentAnswer · Points\nAiSuggestedScore? · FinalScore?\nTeacherComment? · Version"}
    # Main domain associations; labels remain attached to their actual connector.
    rel=[
      ("Course","Question","1 — questions — 0..*",[(900,230),(1140,230)],(1020,205),False),
      ("Course","Rubric","1 — defines — 0..*",[(600,175),(600,145),(210,145),(210,175)],(405,145),False),
      ("Course","ExamSession","1 — schedules — 0..*",[(750,230),(820,320),(820,405),(750,485)],(820,355),False),
      ("User","ExamSession","1 lecturer — creates — 0..*",[(360,485),(600,485)],(480,460),False),
      ("ExamSession","SessionQuestion","1 — contains — 1..*",[(900,485),(1140,485)],(1020,460),False),
      ("Question","SessionQuestion","1 — appears in — 0..*",[(1290,285),(1290,430)],(1290,355),False),
      ("User","Transcript","1 student — submits — 0..*",[(210,540),(210,690)],(210,615),False),
      ("ExamSession","Transcript","1 — records — 0..*",[(700,540),(700,640),(360,640),(360,745)],(530,640),False),
      ("Transcript","TranscriptTurn","1 — turns — 0..*",[(360,745),(600,745)],(480,720),False),
      ("Transcript","Assessment","1 — has — 0..*",[(360,790),(405,825),(1085,825),(1140,790)],(745,825),False),
      ("SessionQuestion","Assessment","1 — scored in — 0..*",[(1290,540),(1290,690)],(1290,610),False),
      ("SessionQuestion","TranscriptTurn","1 — referenced by — 0..*",[(1140,515),(1100,560),(1100,610),(900,610),(750,690)],(1000,610),False),
      ("User","Assessment","0..1 lecturer — finalizes — 0..*",[(60,540),(35,570),(35,980),(1100,980),(1290,820)],(570,980),True),
    ]
    for _,_,label,pts,at,dash in rel: connector(d,pts,label,at,dashed=dash,fs=12,arrow=False)
    for n,xy in p.items():
        x,y=xy; h=112 if n!="Assessment" else 130
        d.rectangle((x,y,x+300,y+h),fill=PAPER,outline=INK,width=2)
        d.rectangle((x,y,x+300,y+34),fill=(246,248,250),outline=INK,width=2)
        d.text((x+150,y+7),n,font=f(17,True),fill=INK,anchor="mt")
        yy=y+43
        for ln in fields[n].split("\n"):
            d.text((x+12,yy),ln,font=f(13),fill=INK); yy+=22
    im.save(IMAGES/"04_Class_Diagram_Current.png",optimize=True)
    nodes=[]
    for n,(x,y) in p.items():
        entries=fields[n].replace("\n","<br>")
        nodes.append({"id":n,"label":f"<b>{n}</b><hr>{entries}","x":x,"y":y,"w":300,"h":130,
          "style":"swimlane;html=1;startSize=32;horizontal=0;rounded=0;fillColor=#FFFFFF;swimlaneFillColor=#F6F8FA;strokeColor=#171D25;fontColor=#171D25;fontFamily=Arial;fontSize=13;"})
    edges=[]
    for i,(a,b,label,pts,at,dash) in enumerate(rel):
        st="edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#171D25;endArrow=none;fontFamily=Arial;fontSize=11;labelBackgroundColor=#FFFFFF;"
        if dash: st+="dashed=1;dashPattern=6 4;"
        edges.append({"id":f"classRel{i}","source":a,"target":b,"label":label,"style":st})
    write_drawio(DOCS/"AIVES-UML-Class-Diagram-remake.drawio","AIVES core domain class diagram",nodes,edges,1800,1100)


def make_state_machines():
    im,d=canvas("AIVES PERSISTED STATE MACHINES","ExamSession and Transcript states match the current backend enums",1800,1050)
    def state_box(x,y,label,w=205):
        d.rounded_rectangle((x,y,x+w,y+68),radius=22,fill=(249,250,251),outline=INK,width=2)
        d.text((x+w/2,y+34),label,font=f(17,True),fill=INK,anchor="mm")
    def initial(x,y): d.ellipse((x-12,y-12,x+12,y+12),fill=INK)
    def final(x,y):
        d.ellipse((x-15,y-15,x+15,y+15),outline=INK,width=2,fill=PAPER)
        d.ellipse((x-9,y-9,x+9,y+9),fill=INK)
    def arrowline(points,label,at):
        connector(d,points,label,at,fs=13,arrow=True)
    d.text((75,160),"EXAM SESSION",font=f(19,True),fill=INK)
    for x,name in [(160,"Draft"),(500,"Open"),(840,"Closed"),(1180,"Cancelled")]: state_box(x,225,name)
    initial(95,259); arrowline([(107,259),(160,259)],"",(130,240))
    arrowline([(365,259),(500,259)],"open",(432,240))
    arrowline([(705,259),(840,259)],"close",(772,240))
    # Cancellation transitions use separate clean rails below the states.
    arrowline([(260,293),(260,360),(1280,360),(1280,293)],"cancel from Draft",(760,360))
    arrowline([(600,293),(600,390),(1280,390),(1280,293)],"cancel from Open",(970,390))
    final(1090,259); arrowline([(1045,259),(1075,259)],"",(1060,240))
    final(1455,259); arrowline([(1385,259),(1440,259)],"",(1410,240))

    d.text((75,475),"TRANSCRIPT / STUDENT ATTEMPT",font=f(19,True),fill=INK)
    xs=[80,385,690,995,1300]
    names=["InProgress","Submitted","Grading","AwaitingReview","Finalized"]
    for x,name in zip(xs,names): state_box(x,550,name,210)
    initial(30,584); arrowline([(42,584),(80,584)],"",(58,566))
    for i,label in enumerate(["submit","grade","AI grading succeeds","all scores finalized"]):
        arrowline([(xs[i]+210,584),(xs[i+1],584)],label,((xs[i]+210+xs[i+1])/2,565))
    arrowline([(795,618),(795,700),(490,700),(490,618)],"AI grading fails; retry",(645,700))
    final(1550,584); arrowline([(1510,584),(1535,584)],"",(1522,566))
    d.rounded_rectangle((80,780,1715,925),radius=12,fill=(250,251,252),outline=(110,120,130),width=2)
    d.text((110,802),"Lifecycle notes",font=f(17,True),fill=INK)
    d.text((110,840),"Closing an ExamSession submits its in-progress attempts. Grading failures return the Transcript to Submitted for retry.",font=f(15),fill=INK)
    d.text((110,875),"Question authoring Draft / Validating are request workflow steps; Question has no persisted Status field.",font=f(15),fill=INK)
    im.save(IMAGES/"05_State_Machines_Current.png",optimize=True)

    nodes=[]
    def node(cid,label,x,y,w,h,style): nodes.append({"id":cid,"label":label,"x":x,"y":y,"w":w,"h":h,"style":style})
    st="rounded=1;arcSize=40;whiteSpace=wrap;html=1;fillColor=#F9FAFB;strokeColor=#171D25;fontColor=#171D25;fontFamily=Arial;fontSize=15;fontStyle=1;"
    dot="ellipse;html=1;fillColor=#171D25;strokeColor=#171D25;"
    end="ellipse;shape=endState;html=1;fillColor=#FFFFFF;strokeColor=#171D25;strokeWidth=2;"
    node("initSession","",83,247,24,24,dot)
    for cid,label,x in [("draft","Draft",160),("open","Open",500),("closed","Closed",840),("cancelled","Cancelled",1180)]: node(cid,label,x,225,205,68,st)
    node("endClosed","",1075,244,30,30,end); node("endCancelled","",1440,244,30,30,end)
    node("initTranscript","",18,572,24,24,dot)
    for cid,label,x in zip(["progress","submitted","grading","review","finalized"],names,xs): node(cid,label,x,550,210,68,st)
    node("endFinal","",1535,569,30,30,end)
    edges=[]
    def trans(a,b,label,extra=""):
        edges.append({"source":a,"target":b,"label":label,"style":"endArrow=open;endSize=10;html=1;strokeColor=#171D25;fontColor=#171D25;fontFamily=Arial;fontSize=13;labelBackgroundColor=#FFFFFF;"+extra})
    trans("initSession","draft",""); trans("draft","open","open"); trans("open","closed","close")
    trans("draft","cancelled","cancel", "edgeStyle=orthogonalEdgeStyle;")
    trans("open","cancelled","cancel", "edgeStyle=orthogonalEdgeStyle;")
    trans("closed","endClosed",""); trans("cancelled","endCancelled","")
    trans("initTranscript","progress",""); trans("progress","submitted","submit"); trans("submitted","grading","grade")
    trans("grading","review","AI grading succeeds"); trans("grading","submitted","failure / retry","edgeStyle=orthogonalEdgeStyle;")
    trans("review","finalized","all scores finalized"); trans("finalized","endFinal","")
    write_drawio(DOCS/"State-Machine-Exam-Lifecycle-remake.drawio","AIVES persisted state machines",nodes,edges,1800,1050)


def make_sequence(title, subtitle, participants, steps, png_name, drawio_name, phase_breaks=()):
    w=1800; top=225; step_h=58; h=max(1040,top+len(steps)*step_h+80)
    im,d=canvas(title,subtitle,w,h)
    left,right=115,w-115; xs=[int(left+i*(right-left)/(len(participants)-1)) for i in range(len(participants))]
    for x,name in zip(xs,participants):
        d.rounded_rectangle((x-94,145,x+94,205),radius=6,fill=(249,250,251),outline=INK,width=2)
        lines=wrap(d,name,f(15,True),170)
        d.multiline_text((x,175),"\n".join(lines),font=f(15,True),fill=INK,anchor="mm",align="center",spacing=1)
        yy=205
        while yy<h-35:
            d.line((x,yy,x,yy+10),fill=(110,120,130),width=2); yy+=18
    for i,(src,dst,label,reply) in enumerate(steps):
        y=top+i*step_h
        if i in phase_breaks:
            d.line((85,y-24,w-85,y-24),fill=(170,178,186),width=1)
        x1,x2=xs[src],xs[dst]
        connector(d,[(x1,y),(x2,y)],label,((x1+x2)/2,y-17),dashed=reply,fs=14)
    im.save(IMAGES/png_name,optimize=True)
    emit_sequence(DOCS/drawio_name,title,participants,steps,w,h)


def write_drawio(path,title,nodes,edges,w,h):
    mx=ET.Element("mxfile",{"host":"app.diagrams.net","modified":"2026-11-01T00:00:00.000Z","agent":"Codex","version":"24.7.17","type":"device"})
    dg=ET.SubElement(mx,"diagram",{"id":"page1","name":title})
    model=ET.SubElement(dg,"mxGraphModel",{"dx":str(w),"dy":str(h),"grid":"1","gridSize":"10","guides":"1","tooltips":"1","connect":"1","arrows":"1","fold":"1","page":"1","pageScale":"1","pageWidth":str(w),"pageHeight":str(h),"math":"0","shadow":"0"})
    root=ET.SubElement(model,"root"); ET.SubElement(root,"mxCell",{"id":"0"}); ET.SubElement(root,"mxCell",{"id":"1","parent":"0"})
    for n in nodes:
        c=ET.SubElement(root,"mxCell",{"id":n["id"],"value":n["label"],"style":n["style"],"vertex":"1","parent":"1"})
        ET.SubElement(c,"mxGeometry",{"x":str(n["x"]),"y":str(n["y"]),"width":str(n["w"]),"height":str(n["h"]),"as":"geometry"})
    for i,e in enumerate(edges):
        c=ET.SubElement(root,"mxCell",{"id":e.get("id",f"edge{i}"),"value":e.get("label",""),"style":e["style"],"edge":"1","parent":"1","source":e["source"],"target":e["target"]})
        ET.SubElement(c,"mxGeometry",{"relative":"1","as":"geometry"})
    path.write_text(ET.tostring(mx,encoding="unicode"),encoding="utf-8")


def emit_sequence(path,title,participants,steps,w,h):
    left,right=115,w-115; xs=[int(left+i*(right-left)/(len(participants)-1)) for i in range(len(participants))]
    nodes=[]; edges=[]
    for i,(name,x) in enumerate(zip(participants,xs)):
        nodes += [{"id":f"p{i}","label":name,"x":x-94,"y":145,"w":188,"h":60,"style":"rounded=0;whiteSpace=wrap;html=1;fillColor=#F9FAFB;strokeColor=#171D25;fontColor=#171D25;fontFamily=Arial;fontSize=15;fontStyle=1;align=center;verticalAlign=middle;"},
                  {"id":f"l{i}t","label":"","x":x-1,"y":205,"w":2,"h":2,"style":"ellipse;fillOpacity=0;strokeOpacity=0;opacity=0;"},
                  {"id":f"l{i}b","label":"","x":x-1,"y":h-40,"w":2,"h":2,"style":"ellipse;fillOpacity=0;strokeOpacity=0;opacity=0;"}]
        edges.append({"source":f"l{i}t","target":f"l{i}b","style":"endArrow=none;startArrow=none;dashed=1;dashPattern=6 5;strokeColor=#777777;strokeWidth=1;html=1;"})
    y=225
    for i,(src,dst,label,reply) in enumerate(steps):
        nodes += [{"id":f"m{i}s","label":"","x":xs[src]-1,"y":y-1,"w":2,"h":2,"style":"ellipse;fillOpacity=0;strokeOpacity=0;opacity=0;"},
                  {"id":f"m{i}t","label":"","x":xs[dst]-1,"y":y-1,"w":2,"h":2,"style":"ellipse;fillOpacity=0;strokeOpacity=0;opacity=0;"}]
        st="endArrow=block;endFill=1;endSize=6;html=1;strokeColor=#171D25;fontFamily=Arial;fontSize=13;labelBackgroundColor=#FFFFFF;"
        if reply: st += "dashed=1;dashPattern=6 4;"
        edges.append({"source":f"m{i}s","target":f"m{i}t","label":label,"style":st})
        y += 58
    write_drawio(path,title,nodes,edges,w,h)


def main():
    IMAGES.mkdir(exist_ok=True)
    make_erd()
    make_sequence("SEQUENCE DIAGRAM — QUESTION AND COURSE RUBRIC MANAGEMENT (FLOW 1)",
      "Separate question and course-rubric endpoints; validation failures return HTTP 400",
      ["Lecturer","Web UI","Questions / Courses API","Application services","EF Core / SQL Server"],
      [(0,1,"Enter question details",False),(1,2,"POST /api/questions",False),(2,3,"Validate content, course and Bloom level",False),(3,4,"Add Question; SaveChanges",False),(4,3,"Saved Question",True),(3,2,"201 Created + QuestionDto",True),(2,1,"HTTP response",True),(0,1,"Edit course rubric matrix",False),(1,2,"PUT /api/courses/{id}/rubrics",False),(2,3,"Validate criteria and total weight = 100%",False),(3,4,"Soft-delete active rows; add replacements",False),(4,3,"Saved rubric matrix",True),(3,2,"CourseDto",True),(2,1,"200 OK",True)],"07_Sequence_Flow1_Current.png","Sequence-CreateQuestion-Rubric-remake-v2.drawio",{7}),
    make_sequence("SEQUENCE DIAGRAM — VIVA INTERVIEW (FLOW 3)",
      "REST attempt and follow-up; voice answers optionally use Azure AI Speech",
      ["Student","Web client","Attempts / Interview / Voice APIs","Application services","Google Gemini API","Azure AI Speech","SQL Server"],
      [(0,1,"Start exam",False),(1,2,"POST /api/exam-sessions/{id}/attempts",False),(2,3,"Validate session; create Transcript",False),(3,6,"Persist InProgress attempt",False),(2,1,"201 Created / 200 OK + AttemptDto",True),(1,2,"GET /api/attempts/{id}/questions",False),(2,3,"Load ordered question snapshot",False),(2,1,"200 OK + questions",True),(0,1,"Submit text answer",False),(1,2,"POST /api/attempts/{id}/answers",False),(2,3,"Validate and save Student turn",False),(3,6,"Persist TranscriptTurn",False),(2,1,"201 Created / 200 OK + TurnDto",True),(0,1,"Request AI follow-up",False),(1,2,"POST /api/attempts/{id}/follow-up",False),(2,3,"Build prompt from saved turns",False),(3,4,"Gemini HTTPS request",False),(4,3,"Follow-up text",True),(3,6,"Persist AI TranscriptTurn",False),(2,1,"201 Created + FollowUpTurnDto",True),(0,1,"Optional voice answer upload",False),(1,2,"POST /voice-answers (multipart)",False),(2,5,"Azure Speech STT request",False),(5,2,"Recognized answer text",True),(2,3,"Save answer and generate follow-up",False),(3,4,"Gemini HTTPS request",False),(4,3,"Follow-up text",True),(3,6,"Persist student and AI turns",False),(3,5,"Azure Speech TTS request",False),(5,2,"Optional audio output",True),(2,1,"Transcript + follow-up + optional audio",True),(0,1,"Submit attempt",False),(1,2,"POST /api/attempts/{id}/submit",False),(2,3,"Set Submitted; persist status",False),(2,1,"200 OK + AttemptDto",True)],"08_Sequence_Flow3_Current.png","Sequence-Flow3-AIViva-Interview-remake-v2.drawio",{8,20,30}),
    make_sequence("SEQUENCE DIAGRAM — AI GRADING AND LECTURER FINALIZATION (FLOW 4)",
      "Lecturer-triggered grading; official result is available after every question is finalized",
      ["Lecturer","Review UI","Grading / Assessments API","Grading service","Google Gemini API","SQL Server","Student"],
      [(0,1,"Select submitted attempt",False),(1,2,"POST /api/attempts/{id}/grade",False),(2,3,"Authorize owner; require Submitted",False),(3,5,"Load turns and rubric snapshots",False),(5,3,"Attempt data",True),(3,4,"Request rubric-based evaluation",False),(4,3,"Scores, feedback, confidence",True),(3,5,"Validate output; calculate and save assessments",False),(5,3,"Saved",True),(3,5,"Set attempt to AwaitingReview",False),(2,1,"202 Accepted + assessments",True),(0,1,"Review AI suggestions",False),(1,2,"GET /api/attempts/{id}/assessments",False),(2,3,"Load ordered assessments",False),(3,5,"Read assessments",False),(5,3,"Assessment list",True),(2,1,"200 OK",True),(0,1,"Enter final score and comment",False),(1,2,"PUT /api/assessments/{id}/final-score",False),(2,3,"Check owner, version and score range",False),(3,5,"Save final score and lecturer audit",False),(3,5,"When all finalized: store total; set Finalized",False),(6,1,"Request finalized result",False),(1,2,"GET /api/attempts/{id}/result",False),(2,3,"Load finalized result",False),(3,5,"Read official result",False),(2,1,"200 OK + official result",True)],"09_Sequence_Flow4_Current.png","Sequence-Flow4-AIGrading-HumanInTheLoop-remake-v2.drawio",{11,17,23})
    make_class_diagram()
    make_state_machines()


if __name__ == "__main__":
    main()
