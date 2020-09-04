# One_a_day
Daily Brain Teasers Web App using React/ React Native


## Overview

My project is a brain teaser/ tenacious puzzle a day to help keep the mind sharp. 
It will provide a new brain teaser each day with variations as to how to solve a problem. Will take suggestions from random users on the website as to what problem they would like  to see next or if they have a problem they would like to submit. Questions will have hints and a solutions button after a 'X' number of attempts. There will also be a QnA section where people can discuss the problem at hand possibly think up more variations for future problems. Will try to implement a images sections to better visualize the problems. There will be a home page and can filter by problem name and possibly number. Each user can have a unique profile with questions succesfully answersed out of the all the questions listed.
This will be strictly mental strength training exercises, so no prequisites will be needed in order to solve a question. Just old fashioned pen, paper and good ol' effort



## Data Model


The application will store Users, Questions, and Solutions

* users will have a list of what questions they have completed
* Questions will all be a little different
* Solutions will always have text and try to guide the user to understand the thought process. Some solutions may include images *Research how to implement .png*

SAMPLE DOCUMENTS:

An Example User:

```javascript
{
  username: "shannonshopper",
  hash: // a password hash,
}
```

An Example List with Embedded Items:

```javascript
{
  user: // a reference to a User object
  questions_num: //how many questions they have successfully answered
  contribution_score: //how many questions they have suggested and been actually implemented
  createdAt: // timestamp
}
//more will be added as this project flows along
```


## [Link to Commented First Draft Schema](db.js)

(___TODO__: create a first draft of your Schemas in db.js and link to it_)

## Wireframes

###TODO
(___TODO__: wireframes for all of the pages on your site; they can be as simple as photos of drawings or you can use a tool like Balsamiq, Omnigraffle, etc._)

/cook/add adding a recipe to the homepage

![adding recipe](WireFraming/Add-Recipe.png)

/home - page for showing all recipes

![homepage](WireFraming/HOMEPAGE.PNG)

/cook/login - login/user creation page

![list](WireFraming/Login-Page.png)

## Site map

(___TODO__: draw out a site map that shows how pages are related to each other_)

Here's a [complex example from wikipedia](https://upload.wikimedia.org/wikipedia/commons/2/20/Sitemap_google.jpg), but you can create one without the screenshots, drop shadows, etc. ... just names of pages and where they flow to.

![list](WireFraming/SiteMap.png)

## User Stories or Use Cases

1. As a non-registered user you can create an account
2. As a non-registered user you can answer questions still but will not have a score
3. As a user you can post in the comments section and maintain a score
4. If your submitted question is selected, will receieve mention/shoutout for it
5. As a user you can see what question you have contributed



## Research Topics


1. React Native

2. React Router 

3. Swift Implementation??? via IOS

4. 


## [Link to Initial Main Project File](app.js) 

(___TODO__: create a skeleton Express application with a package.json, app.js, views folder, etc. ... and link to your initial app.js_) (Done)

## Annotations / References Used

(___TODO__: list any tutorials/references/etc. that you've based your code off of_)

1. [passport.js authentication docs](http://passportjs.org/docs) - (add link to source code that was based on this)
2. [tutorial on vue.js](https://vuejs.org/v2/guide/) - (add link to source code that was based on this)
^^^ Will fix
^^^ Please refer to the above 
